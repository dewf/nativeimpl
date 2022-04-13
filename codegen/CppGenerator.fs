[<RequireQualifiedAccess>]
module CppGenerator

open System
open System.IO
open System.Text.RegularExpressions
open AbstractSyntaxTree

let PopTemplate = "std::shared_ptr<%%IFACE%%> %%IFACE%%__pop()
{
    bool isClientID;
    auto id = ni_popInstance(&isClientID);
    if (isClientID) {
        return std::shared_ptr<%%IFACE%%>(new Client%%IFACE%%(id));
    }
    else {
        return Server%%IFACE%%::getByID(id);
    }
}"
let PushTemplate = "inline void %%IFACE%%__push(std::shared_ptr<%%IFACE%%> inst, bool isReturn) {
    auto pushable = std::dynamic_pointer_cast<Pushable>(inst);
    pushable->push(pushable, isReturn);
}"
let ServerWrapperTemplate = "class Server%%IFACE%% : public %%IFACE%%, public ServerObject {
public:
    virtual ~Server%%IFACE%%() {
    }
    static std::shared_ptr<Server%%IFACE%%> getByID(int id) {
        auto obj = ServerObject::getByID(id);
        return std::static_pointer_cast<Server%%IFACE%%>(obj);
    }
};"

let rec typeToString = function
    | UnitType -> "void"
    | Bool -> "bool"
    | Int32 -> "int"
    | InterfaceType name -> sprintf "std::shared_ptr<%s>" name
    | ExceptionType name -> name
    | AnonymousFunction (argTypes, returnType) ->
        let joinedTypes =
            [for t in argTypes -> typeToString t]
            |> String.concat ","
        sprintf "std::function<%s(%s)>" (typeToString returnType) joinedTypes
    | _ -> "(typeToString: not done yet)"

let pushFunctionTypedef (writer: TextWriter) (level: int) (name: string) (args: NIType list) (ret: NIType) =
    let indent = String.replicate level "    "
    let argsJoined =
        [for arg in args -> typeToString arg]
        |> String.concat ", "
    fprintfn writer "%stypedef %s %s(%s);" indent (typeToString ret) name argsJoined

let joinedFunctionArgs (nf: NamedFunction) =
    [for argName, argType in nf.Args -> sprintf "%s %s" (typeToString argType) argName]
    |> String.concat ", "

let joinedFields (fields: NamedField list) =
    [for field in fields -> sprintf "%s %s" (typeToString field.Type_) field.Name]
    |> String.concat ", "

let joinedAnonymousArgs (args: NIType list) =
    [for i, argType in args |> List.indexed -> sprintf "%s _%d" (typeToString argType) i]
    |> String.concat ", "

let rec defaultValueForType = function
    | UnitType -> failwith "defaultValueForType called on UnitType"
    | Bool -> "false"
    | Int32 -> "0"
    | InterfaceType _ -> "null"
    | AnonymousFunction (niTypes, niType) ->
        sprintf "[](%s) { return %s; }" (joinedAnonymousArgs niTypes) (defaultValueForType niType)
    | _ ->
        "(defaultValueForType: not yet supported)"

type PopValueContext =
    | Variable of name: string
    | Return

let rec popValue (writer: TextWriter) (level: int) (type_: NIType) (context: PopValueContext) =
    let indent = String.replicate level "    "
    match type_ with
    | UnitType ->
        match context with
        | Return -> ()  // empty!
        | Variable _ ->
            failwithf "tried to pop a Unit value into a variable, why?"
    | Bool ->
        match context with
        | Variable name ->
            fprintfn writer "%sauto %s = ni_popBool();" indent name
        | Return ->
            fprintfn writer "%sreturn ni_popBool();" indent
    | Int32 ->
        match context with
        | Variable name ->
            fprintfn writer "%sauto %s = ni_popInt();" indent name
        | Return ->
            fprintfn writer "%sreturn ni_popInt();" indent
    | InterfaceType ifaceName ->
        match context with
        | Variable name ->
            fprintfn writer "%sauto %s = %s__pop();" indent name ifaceName
        | Return ->
            fprintfn writer "%sreturn %s__pop();" indent ifaceName
    | AnonymousFunction (argTypes, retType) ->
        let name =
            match context with
            | Variable name -> name
            | Return -> "__ret"
        fprintfn writer "%sauto %s__id = ni_popClientFunc();" indent name
        fprintfn writer "%sauto %s__cf = std::shared_ptr<ClientFunc>(new ClientFunc(%s__id));" indent name name
        fprintfn writer "%sauto %s = [%s__cf](%s) {" indent name name (joinedAnonymousArgs argTypes)
        // push those inner params in reverse order
        argTypes
        |> List.rev
        |> List.iteri (fun i type_ -> pushValue writer (level + 1) type_ (sprintf "_%d" i) false)
        fprintfn writer "%s    %s__cf->remoteExec();" indent name
        popValue writer (level + 1) retType Return
        fprintfn writer "%s};" indent
    | _ ->
        fprintfn writer "%s(not yet implemented);" indent

and pushValue (writer: TextWriter) (level: int) (type_: NIType) (content: string) (isReturn: bool) =
    let indent = String.replicate level "    "
    match type_ with
    | UnitType ->
        fprintfn writer "%s%s;" indent content // no push!
    | Bool ->
        fprintfn writer "%sni_pushBool(%s);" indent content
    | Int32 ->
        fprintfn writer "%sni_pushInt(%s);" indent content
    | InterfaceType name ->
        fprintfn writer "%s%s__push(%s, %s);" indent name content (if isReturn then "true" else "false")
    | AnonymousFunction (argTypes, returnType) ->
        fprintfn writer "%sauto __f = %s;" indent content
        // unique key bullshit
        fprintfn writer "%ssize_t __uniqueKey = 0;" indent
        fprintfn writer "%sif (__f) {" indent
        pushFunctionTypedef writer (level + 1) "function_t" argTypes returnType
        fprintfn writer "%s    function_t* ptr_fun = __f.target<function_t>();" indent
        fprintfn writer "%s    if (ptr_fun != nullptr) {" indent
        fprintfn writer "%s        uniqueKey = (size_t)ptr_fun;" indent
        fprintfn writer "%s    }" indent
        fprintfn writer "%s}" indent
        // wrapper:
        fprintfn writer "%sauto __wrapper = [__f]() {" indent
        argTypes
        |> List.iteri (fun i argType ->
            popValue writer (level + 1) argType (Variable (sprintf "_%d" i) ))
        let argList =
            argTypes
            |> List.mapi (fun i _ -> sprintf "_%d" i)
            |> String.concat ", "
        pushValue writer (level + 1) returnType (sprintf "__f(%s)" argList) true
        fprintfn writer "%s};" indent
        // end wrapper
        fprintfn writer "%spushServerFuncVal(__wrapper, __uniqueKey);" indent
    | _ ->
        fprintfn writer "%s(pushValue not supported yet)" indent

let lowerFirst (text: string) =
    Char.ToLower(text[0]).ToString() + text.Substring(1)

let generateWrappersCpp (m: Module) (writer: TextWriter) =
    fprintfn writer "#include \"NativeImplServer.h\""
    fprintfn writer "#include \"Implementation.h\""
    fprintfn writer ""
    // things we'll need to use a few times:
    let globals =
        m.Defs
        |> List.choose (function
            | Global(name, niType) -> Some (name, niType)
            | _ -> None)
    let userTypes =
        m.Defs
        |> List.choose (function
            | UserType(name, userType) -> Some (name, userType)
            | _ -> None)
    let moduleMethods =
        m.Defs
        |> List.choose (function
            | FunctionDef namedFunction -> Some namedFunction
            | _ -> None)
    let interfaces =
        m.Defs
        |> List.choose (function
            | UserType(name, InterfaceDef namedFunctions) -> Some (name, namedFunctions)
            | _ -> None)
    let exceptions =
        m.Defs
        |> List.choose (function
            | UserType(name, ExceptionDef fields) -> Some (name, fields)
            | _ -> None)
    // begin output: ===============================================

    // opaques + interface method + exception handles
    for name, userType in userTypes do
        let lowerName = lowerFirst name
        match userType with
        | OpaqueHandleDef ->
            fprintfn writer "NIHANDLE(%s);" lowerName
        | InterfaceDef namedFunctions ->
            namedFunctions
            |> List.iter (fun nf ->
                fprintfn writer "ni_InterfaceMethodRef %s_%s;" lowerName nf.Name)
        | ExceptionDef _ ->
            fprintfn writer "ni_ExceptionRef %s;" lowerName
        | _ ->
            ()

    fprintfn writer ""

    // interfaces: fwd decls, client wrapper, push/pop
    for name, methods in interfaces do
        // fwd decls used in client wrapper
        fprintfn writer "void %s__push(std::shared_ptr<%s> thing, bool isReturn);" name name
        fprintfn writer "std::shared_ptr<%s> %s__pop();" name name
        fprintfn writer ""
        // client wrapper
        fprintfn writer "class Client%s : public ClientObject, public %s {" name name
        fprintfn writer "public:"
        fprintfn writer "    Client%s(int id) : ClientObject(id) {}" name
        // methods
        for namedFunc in methods do
            let argList =
                [for argName, argType in namedFunc.Args -> sprintf "%s %s" (typeToString argType) argName]
                |> String.concat ", "
            fprintfn writer "    %s %s(%s) override {" (typeToString namedFunc.Returns) namedFunc.Name argList
            // push args (reverse order)
            for argName, argType in namedFunc.Args |> List.rev do
                pushValue writer 2 argType argName false
            // invoke
            match namedFunc.Throws with
            | Some _ ->
                fprintfn writer "        invokeMethodWithExceptions(%s_%s);" (lowerFirst name) namedFunc.Name
            | None ->
                fprintfn writer "        invokeMethod(%s_%s);" (lowerFirst name) namedFunc.Name
            // pop result
            match namedFunc.Returns with
            | InterfaceType _ | AnonymousFunction _ ->
                // any sort of reference that might get freed if it was only a return value from the other side (stored nowhere else, so protected by the safety area)
                popValue writer 2 namedFunc.Returns (Variable "__ret")
                fprintfn writer "        ni_clearClientSafetyArea();"
                fprintfn writer "        return __ret;"
            | _ ->
                popValue writer 2 namedFunc.Returns Return
            fprintfn writer "    }"
        // end
        fprintfn writer "};"
        fprintfn writer ""
        // push/pop
        let nameRegex = Regex(@"%%IFACE%%")
        writer.WriteLine(nameRegex.Replace(PushTemplate, name))
        writer.WriteLine()
        writer.WriteLine(nameRegex.Replace(PopTemplate, name))
    fprintfn writer ""

    // plain function wrappers
    for namedFunction in moduleMethods do
        fprintfn writer "void %s__wrapper() {" namedFunction.Name
        for argName, argType in namedFunction.Args do
            popValue writer 1 argType (Variable argName)
        let argsJoined =
            [for name, _ in namedFunction.Args -> name]
            |> String.concat ", "
        let invocation =
            sprintf "%s(%s)" namedFunction.Name argsJoined
        match namedFunction.Throws with
        | Some (ExceptionType exceptionName) ->
            fprintfn writer "    try {"
            pushValue writer 2 namedFunction.Returns invocation true
            fprintfn writer "    }"
            fprintfn writer "    catch (const %s& e) {" exceptionName
            fprintfn writer "        ni_setException(%s);" (lowerFirst exceptionName)
            fprintfn writer "        e.push();"
            fprintfn writer "    }"
        | _ -> // note we're not handling Some (non-exception types)! need to be errored out on type checking ...
            pushValue writer 1 namedFunction.Returns invocation true
        fprintfn writer "}"
        fprintfn writer ""

    // interface wrappers
    for name, namedFunctions in interfaces do
        namedFunctions
        |> List.iter (fun nf ->
            fprintfn writer "void %s_%s__wrapper(int serverID) {" name nf.Name
            fprintfn writer "    auto inst = Server%s::getByID(serverID);" name
            for argName, argType in nf.Args do
                popValue writer 1 argType (Variable argName)
            let argsJoined =
                [for name, _ in nf.Args -> name]
                |> String.concat ", "
            let invocation =
                sprintf "inst->%s(%s)" nf.Name argsJoined
            match nf.Throws with
            | Some (ExceptionType exceptionName) ->
                fprintfn writer "    try {"
                pushValue writer 2 nf.Returns invocation true
                fprintfn writer "    }"
                fprintfn writer "    catch (const %s& e) {" exceptionName
                fprintfn writer "        ni_setException(%s);" (lowerFirst exceptionName)
                fprintfn writer "        e.push();"
                fprintfn writer "    }"
            | _ -> // note we're not handling Some (non-exception types)! need to be errored out on type checking ...
                pushValue writer 1 nf.Returns invocation true
            fprintfn writer "}"
            fprintfn writer "")

    // globals/constants func
    fprintfn writer "void __constantsFunc() {"
    // push in reverse order
    for globalName, globalType in globals |> List.rev do
        pushValue writer 1 globalType globalName false // hmm for isReturn? I guess not because the variable is global and not going anywhere, so it doesn't need to be in the safe area
    fprintfn writer "}"

    fprintfn writer ""

    // library entry point / registrations
    fprintfn writer "extern \"C\" int nativeLibraryInit() {"
    fprintfn writer "    auto m = ni_registerModule(\"%s\");" m.Name
    fprintfn writer "    ni_registerModuleConstants(m, &__constantsFunc);"
    // module methods
    for namedFunction in moduleMethods do
        fprintfn writer "    ni_registerModuleMethod(m, \"%s\", &%s__wrapper);" namedFunction.Name namedFunction.Name
    // interfaces + methods
    for name, methods in interfaces do
        let lowerName = lowerFirst name
        fprintfn writer "    auto %s = ni_registerInterface(m, \"%s\");" lowerName name
        for method in methods do
            fprintfn writer "    %s_%s = ni_registerInterfaceMethod(%s, \"%s\", &%s_%s__wrapper);" lowerName method.Name lowerName method.Name lowerName method.Name
    // exceptions
    for name, _ in exceptions do
        let lowerName = lowerFirst name
        fprintfn writer "    %s = ni_registerException(m, \"%s\", &%s::buildAndThrow);" lowerName name name
    // done
    fprintfn writer "    return 0; // = OK"
    fprintfn writer "}"
    fprintfn writer ""
    // shutdown
    fprintfn writer "extern \"C\" void nativeLibraryShutdown() {"
    // nothing for C++
    fprintfn writer "}"

let generateImplementationH (m: Module) (writer: TextWriter) =
    let interfaces =
        m.Defs
        |> List.choose (function
            | UserType(name, InterfaceDef namedFunctions) -> Some (name, namedFunctions)
            | _ -> None)
    let globals =
        m.Defs
        |> List.choose (function
            | Global(name, niType) -> Some (name, niType)
            | _ -> None)
    let moduleMethods =
        m.Defs
        |> List.choose (function
            | FunctionDef namedFunction -> Some namedFunction
            | _ -> None)
    let exceptions =
        m.Defs
        |> List.choose (function
            | UserType(name, ExceptionDef fields) -> Some (name, fields)
            | _ -> None)
    // begin output: ===============================================
    fprintfn writer "#include \"NativeImplServer.h\""
    fprintfn writer "#include <functional>"
    fprintfn writer "#include <memory>"
    fprintfn writer ""
    // exceptions
    for exName, exFields in exceptions do
        fprintfn writer "class %s {" exName
        fprintfn writer "public:"
        for namedField in exFields do
            fprintfn writer "    %s %s;" (typeToString namedField.Type_) namedField.Name
        let joinedInitializers =
            [for nf in exFields -> sprintf "%s(%s)" nf.Name nf.Name]
            |> String.concat ", "
        fprintfn writer "    %s(%s) : %s {}" exName (joinedFields exFields) joinedInitializers
        fprintfn writer "    void push() const {"
        // push field values (reverse order)
        for namedField in exFields |> List.rev do
            pushValue writer 2 namedField.Type_ namedField.Name false // false = not a concern because it still exists in this class instance
        fprintfn writer "    }"
        // build and throw
        fprintfn writer "    static void buildAndThrow() {"
        for i, namedField in exFields |> List.indexed do
            popValue writer 2 namedField.Type_ (Variable (sprintf "_%d" i))
        // joined variables (_0, _1, etc)
        let joinedVars =
            [for i in 0 .. exFields.Length-1 -> sprintf "_%d" i]
            |> String.concat ", "
        fprintfn writer "        throw %s(%s);" exName joinedVars
        fprintfn writer "    }"
        fprintfn writer "};"
        fprintfn writer ""
    // raw interfaces + server wrappers
    for name, methods in interfaces do
        fprintfn writer "class %s {" name
        fprintfn writer "public:"
        for namedFunc in methods do
            let throwsComment =
                match namedFunc.Throws with
                | Some exceptionType -> sprintf "  // throws %s" (typeToString exceptionType)
                | None -> ""
            fprintfn writer "    virtual %s %s(%s) = 0;%s" (typeToString namedFunc.Returns) namedFunc.Name (joinedFunctionArgs namedFunc) throwsComment
        fprintfn writer "};"
        fprintfn writer ""
        // server wrapper
        let nameRegex = Regex(@"%%IFACE%%")
        writer.WriteLine(nameRegex.Replace(ServerWrapperTemplate, name))
        fprintfn writer ""
    // global externs
    for globalName, globalType in globals do
        fprintfn writer "extern const %s %s;" (typeToString globalType) globalName
    printfn ""
    // function prototypes
    for namedFunc in moduleMethods do
        let throwsComment =
            match namedFunc.Throws with
            | Some exceptionType -> sprintf "  // throws %s" (typeToString exceptionType)
            | None -> ""
        fprintfn writer "%s %s(%s);%s" (typeToString namedFunc.Returns) namedFunc.Name (joinedFunctionArgs namedFunc) throwsComment

let generateImplementationCpp (m: Module) (writer: TextWriter) =
    let globals =
        m.Defs
        |> List.choose (function
            | Global(name, niType) -> Some (name, niType)
            | _ -> None)
    let moduleMethods =
        m.Defs
        |> List.choose (function
            | FunctionDef namedFunction -> Some namedFunction
            | _ -> None)
    // begin output: ===============================================
    fprintfn writer "#include \"Implementation.h\""
    fprintfn writer "#include <memory>"
    fprintfn writer "#include <functional>"
    fprintfn writer ""
    // globals
    for globalName, globalType in globals do
        fprintfn writer "const %s %s = %s;" (typeToString globalType) globalName (defaultValueForType globalType)
    fprintfn writer ""
    // functions
    for namedFunc in moduleMethods do
        let throwsComment =
            match namedFunc.Throws with
            | Some exceptionType -> sprintf "  // throws %s" (typeToString exceptionType)
            | None -> ""
        fprintfn writer "%s %s(%s) {%s" (typeToString namedFunc.Returns) namedFunc.Name (joinedFunctionArgs namedFunc) throwsComment
        if namedFunc.Returns <> UnitType then
            fprintfn writer "    return %s;" (defaultValueForType namedFunc.Returns)
        fprintfn writer "}"
        fprintfn writer ""

let generate (m: Module) =
    fprintfn stdout "======= Wrappers.cpp ============================="
    generateWrappersCpp m stdout
    fprintfn stdout ""
    fprintfn stdout "======= Implementation.h ========================="
    generateImplementationH m stdout
    fprintfn stdout ""
    fprintfn stdout "======= Implementation.cpp ======================="
    generateImplementationCpp m stdout
    fprintfn stdout ""
