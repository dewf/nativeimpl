[<RequireQualifiedAccess>]
module CppGenerator

open System.IO
open System.Net.Mime
open System.Text.RegularExpressions
open Parser

open Util

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
    | StringType -> "std::string"
    | InterfaceType name -> sprintf "std::shared_ptr<%s>" name
    | ExceptionType name -> name
    | DelegateType name -> sprintf "std::function<%s>" name
    | ArrayType niType -> sprintf "std::vector<%s>" (typeToString niType)
    | MapType (keyType, valueType) -> sprintf "std::map<%s,%s>" (typeToString keyType) (typeToString valueType)
    | StructType name -> name
    | TypedefType name -> name
    | x -> sprintf "(typeToString: not done yet [%A])" x

let pushFunctionTypedef (writer: TextWriter) (level: int) (name: string) (args: NIType list) (ret: NIType) =
    let indent = String.replicate level "    "
    let argsJoined =
        [for arg in args -> typeToString arg]
        |> String.concat ", "
    fprintfn writer "%stypedef %s %s(%s);" indent (typeToString ret) name argsJoined

let joinedFields (fields: NamedField list) =
    [for field in fields -> sprintf "%s %s" (typeToString field.Type_) field.Name]
    |> String.concat ", "

let joinedFieldNames (fields: NamedField list) =
    [for field in fields -> field.Name]
    |> String.concat ", "

let joinedArgs (args: (string * NIType) list) =
    [for name, type_ in args -> sprintf "%s %s" (typeToString type_) name]
    |> String.concat ", "

let joinedArgNames (args: (string * NIType) list) =
    args
    |> List.map fst
    |> String.concat ", "

let rec defaultValueForType = function
    | UnitType -> failwith "defaultValueForType called on UnitType"
    | Bool -> "false"
    | Int32 -> "0"
    | StringType -> "\"default\""
    | InterfaceType name -> sprintf "std::shared_ptr<%s>()" name
    | DelegateType name -> sprintf "std::function<%s>()" name
    | ArrayType niType -> sprintf "std::vector<%s>()" (typeToString niType)
    | MapType (keyType, valueType) -> sprintf "std::map<%s,%s>()" (typeToString keyType) (typeToString valueType)
    | _ ->
        "(defaultValueForType: not yet supported)"

type PopValueContext =
    | Variable of name: string
    | Return

let rec popValue (writer: TextWriter) (level: int) (type_: NIType) (context: PopValueContext) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    match type_ with
    | UnitType ->
        match context with
        | Return -> ()  // empty!
        | Variable _ ->
            failwithf "tried to pop a Unit value into a variable, why?"
    | Bool ->
        match context with
        | Variable name ->
            write level "auto %s = ni_popBool();" name
        | Return ->
            write level "return ni_popBool();"
    | Int32 ->
        match context with
        | Variable name ->
            write level "auto %s = ni_popInt();" name
        | Return ->
            write level "return ni_popInt();"
    | StringType ->
        match context with
        | Variable name ->
            write level "auto %s = popStringInternal();" name
        | Return ->
            write level "return popStringInternal();"
    | InterfaceType ifaceName ->
        match context with
        | Variable name ->
            write level "auto %s = %s__pop();" name ifaceName
        | Return ->
            write level "return %s__pop();" ifaceName
    | DelegateType delegateName ->
        match context with
        | Variable name ->
            write level "auto %s = %s__pop();" name delegateName
        | Return ->
            write level "return %s__pop();" delegateName
    | ArrayType niType ->
        let popFunc =
            match niType with
            | Bool -> "popBoolArrayInternal"
            | Int32 -> "popIntArrayInternal"
            | StringType -> "popStringArrayInternal"
            | _ -> "(not yet supported)"
        match context with
        | Variable name ->
            write level "auto %s = %s();" name popFunc
        | Return ->
            write level "return %s();" popFunc
    | _ ->
        write level "(not yet implemented)"

and pushValue (writer: TextWriter) (level: int) (type_: NIType) (content: string) (isReturn: bool) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    match type_ with
    | UnitType ->
        write level "%s;" content // no push!
    | Bool ->
        write level "ni_pushBool(%s);" content
    | Int32 ->
        write level "ni_pushInt(%s);" content
    | StringType ->
        write level "pushStringInternal(%s);" content
    | InterfaceType name ->
        write level "%s__push(%s, %s);" name content (if isReturn then "true" else "false")
    | DelegateType name ->
        write level "%s__push(%s);" name content // still hmm about safety area for functions ...
    | ArrayType niType ->
        let pushFunc =
            match niType with
            | Bool -> "pushBoolArrayInternal"
            | Int32 -> "pushIntArrayInternal"
            | StringType -> "pushStringArrayInternal"
            | _ -> "(not yet supported)"
        write level "%s(%s);" pushFunc content
    | _ ->
        write level "(pushValue not supported yet)"

[<RequireQualifiedAccess>]
type UserOrGeneric =
    | User of UserType
    | Generic of NIType

let userTypesAndGenerics (m: Module) =
    // combined map
    let m1 =
        (Map.empty, getUserTypes m)
        ||> Map.fold (fun newMap name userType -> newMap.Add(name, UserOrGeneric.User userType))
    (m1, allGenericTypes m)
    ||> List.fold (fun newMap type_ ->
        let name =
            (typeToString type_)
        newMap.Add(name, UserOrGeneric.Generic type_))

let renderUserTypes (writer: TextWriter) (typeMap: Map<string, UserOrGeneric>) (renderFunc: TextWriter -> string -> UserOrGeneric -> unit) =
    let rec renderType (type_: NIType) (alreadyRendered: Set<string>) =
        match type_ with
        | OpaqueHandleType name -> renderDeps name alreadyRendered
        | StructType name -> renderDeps name alreadyRendered
        | InterfaceType name -> renderDeps name alreadyRendered
        | ExceptionType name -> renderDeps name alreadyRendered
        | DelegateType name -> renderDeps name alreadyRendered
        | TypedefType name -> renderDeps name alreadyRendered
        | ArrayType _ ->
            let name = (typeToString type_)
            renderDeps name alreadyRendered
        | MapType _ ->
            let name = (typeToString type_)
            renderDeps name alreadyRendered
        | _ ->
            alreadyRendered
    and renderDeps (name: string) (alreadyRendered: Set<string>) =
        if alreadyRendered.Contains(name) then
            alreadyRendered
        else
            let content = typeMap.[name]
            let subRendered =
                match content with
                | UserOrGeneric.Generic gType ->
                    match gType with
                    | ArrayType niType ->
                        renderType niType alreadyRendered
                    | MapType (keyType, valueType) ->
                        renderType keyType alreadyRendered
                        |> renderType valueType
                    | _ ->
                        failwithf "renderUserTypes.renderDeps: unsupported generic type %A!" gType
                | UserOrGeneric.User userType ->
                    match userType with
                    | OpaqueHandleDef -> // no sub fields
                        alreadyRendered
                    | StructDef namedFields ->
                        (alreadyRendered, namedFields)
                        ||> List.fold (fun already2 field -> renderType field.Type_ already2)
                    | ExceptionDef namedFields ->
                        (alreadyRendered, namedFields)
                        ||> List.fold (fun already2 field -> renderType field.Type_ already2)
                    | InterfaceDef namedFunctions ->
                        (alreadyRendered, namedFunctions)
                        ||> List.fold (fun already2 namedFunc ->
                            let withReturn =
                                renderType namedFunc.Returns already2
                            let withThrow =
                                match namedFunc.Throws with
                                | Some etype ->
                                    renderType etype withReturn
                                | None ->
                                    withReturn
                            (withThrow, namedFunc.Args)
                            ||> List.fold (fun already3 (_, argType) ->
                                renderType argType already3))
                    | DelegateDef info ->
                        let withReturn =
                            renderType info.Returns alreadyRendered
                        (withReturn, info.Args)
                        ||> List.fold (fun already2 (_, argType) ->
                            renderType argType already2)
                    | TypeDef actual ->
                        renderType actual alreadyRendered

            // use plugin render func, since it depends on our context
            renderFunc writer name content

            // done
            subRendered.Add(name)
    // start from the bottom
    let keys =
        typeMap
        |> Map.toList
        |> List.map fst
    (Set.empty, keys)
    ||> List.fold (fun alreadyRendered k -> renderDeps k alreadyRendered)
    |> ignore

let renderWrapperDelegate (writer: TextWriter) (name: string) (info: DelegateInfo) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""
    // push
    blank()
    write 0 "void %s__push(std::function<%s> f) {" name name
    write 1 "size_t uniqueKey = 0;"
    write 1 "if (f) {"
    write 2 "%s* ptr_fun = f.target<%s>();" name name
    write 2 "if (ptr_fun != nullptr) {"
    write 3 "uniqueKey = (size_t)ptr_fun;"
    write 2 "}"
    write 1 "}"
    write 1 "auto wrapper = [f]() {"
    for argName, argType in info.Args do
        popValue writer 2 argType (Variable argName)
    let invocation =
        sprintf "f(%s)" (joinedArgNames info.Args)
    pushValue writer 2 info.Returns invocation true
    write 1 "};"
    write 1 "pushServerFuncVal(wrapper, uniqueKey);"
    write 0 "}"
    // pop
    blank()
    write 0 "std::function<%s> %s__pop() {" name name
    write 1 "auto id = ni_popClientFunc();"
    write 1 "auto cf = std::shared_ptr<ClientFunc>(new ClientFunc(id));"
    write 1 "auto wrapper = [cf](%s) {" (joinedArgs info.Args)
    for argName, argType in info.Args |> List.rev do
        pushValue writer 2 argType argName false
    write 2 "cf->remoteExec();"
    popValue writer 2 info.Returns Return
    write 1 "};"
    write 1 "return wrapper;"
    write 0 "}"

let renderWrapperInterface (writer: TextWriter) (name: string) (methods: NamedFunction list) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""
    blank()
    // fwd decls used in client wrapper
    write 0 "void %s__push(std::shared_ptr<%s> thing, bool isReturn);" name name
    write 0 "std::shared_ptr<%s> %s__pop();" name name
    blank()
    // client wrapper
    write 0 "class Client%s : public ClientObject, public %s {" name name
    write 0 "public:"
    write 1 "Client%s(int id) : ClientObject(id) {}" name
    // methods
    for namedFunc in methods do
        let argList =
            [for argName, argType in namedFunc.Args -> sprintf "%s %s" (typeToString argType) argName]
            |> String.concat ", "
        write 1 "%s %s(%s) override {" (typeToString namedFunc.Returns) namedFunc.Name argList
        // push args (reverse order)
        for argName, argType in namedFunc.Args |> List.rev do
            pushValue writer 2 argType argName false
        // invoke
        match namedFunc.Throws with
        | Some _ ->
            write 2 "invokeMethodWithExceptions(%s_%s);" (lowerFirst name) namedFunc.Name
        | None ->
            write 2 "invokeMethod(%s_%s);" (lowerFirst name) namedFunc.Name
        // pop result
        match namedFunc.Returns with
        | InterfaceType _ -> // | AnonymousFunction _
            // any sort of reference that might get freed if it was only a return value from the other side (stored nowhere else, so protected by the safety area)
            popValue writer 2 namedFunc.Returns (Variable "__ret")
            write 2 "ni_clearClientSafetyArea();"
            write 2 "return __ret;"
        | _ ->
            popValue writer 2 namedFunc.Returns Return
        write 1 "}"
    // end
    write 0 "};"
    blank()
    // push/pop
    let nameRegex = Regex(@"%%IFACE%%")
    writer.WriteLine(nameRegex.Replace(PushTemplate, name))
    writer.WriteLine()
    writer.WriteLine(nameRegex.Replace(PopTemplate, name))

let renderWrapperException (writer: TextWriter) (name: string) (exFields: NamedField list) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""
    blank()
    write 0 "void %s__push(%s e) {" name name
    // push field values (reverse order)
    for namedField in exFields |> List.rev do
        let memberVar =
            sprintf "e.%s" namedField.Name
        pushValue writer 1 namedField.Type_ memberVar false // false = not a concern because it still exists in this class instance
    write 0 "}"
    // build and throw
    blank()
    write 0 "void %s__buildAndThrow() {" name
    for field in exFields do
        popValue writer 1 field.Type_ (Variable field.Name)
    // joined variables (_0, _1, etc)
    write 1 "throw %s(%s);" name (joinedFieldNames exFields)
    write 0 "}"

let renderWrapperUserOrGeneric (writer: TextWriter) (name: string) (content: UserOrGeneric) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    match content with
    | UserOrGeneric.Generic gType ->
        write 0 "// push/pop for generic %s" (typeToString gType)
    | UserOrGeneric.User userType ->
        match userType with
        | OpaqueHandleDef ->
            write 0 "// anything for opaque here? push/pop perhaps? %s" name
        | StructDef namedFields ->
            write 0 "// push/pop for struct %s" name
        | ExceptionDef namedFields ->
            renderWrapperException writer name namedFields
        | InterfaceDef methods ->
            renderWrapperInterface writer name methods
        | DelegateDef info ->
            renderWrapperDelegate writer name info
        | TypeDef niType ->
            write 0 "// push/pop for typedef %s [%s] ??" name (typeToString niType)

let generateWrappersCpp (m: Module) (writer: TextWriter) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""
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
    // prologue
    write 0 "#include \"NativeImplServer.h\""
    write 0 "#include \"Implementation.h\""

    // opaques + interface method + exception handles (for registration storage)
    blank()
    for name, userType in userTypes do
        let lowerName = lowerFirst name
        match userType with
        | OpaqueHandleDef ->
            write 0 "NIHANDLE(%s);" lowerName
        | InterfaceDef namedFunctions ->
            namedFunctions
            |> List.iter (fun nf ->
                write 0 "ni_InterfaceMethodRef %s_%s;" lowerName nf.Name)
        | ExceptionDef _ ->
            write 0 "ni_ExceptionRef %s;" lowerName
        | _ ->
            ()

    // user types and generics in dependency order
    renderUserTypes writer (userTypesAndGenerics m) renderWrapperUserOrGeneric

    // plain function wrappers
    for namedFunction in moduleMethods do
        blank()
        write 0 "void %s__wrapper() {" namedFunction.Name
        for argName, argType in namedFunction.Args do
            popValue writer 1 argType (Variable argName)
        let invocation =
            sprintf "%s(%s)" namedFunction.Name (joinedArgNames namedFunction.Args)
        match namedFunction.Throws with
        | Some (ExceptionType exceptionName) ->
            write 1 "try {"
            pushValue writer 2 namedFunction.Returns invocation true
            write 1 "}"
            write 1 "catch (const %s& e) {" exceptionName
            write 2 "ni_setException(%s);" (lowerFirst exceptionName)
            write 2 "%s__push();" exceptionName
            write 1 "}"
        | _ -> // note we're not handling Some (non-exception types)! need to be errored out on type checking ...
            pushValue writer 1 namedFunction.Returns invocation true
        write 0 "}"

    // interface wrappers
    for name, namedFunctions in interfaces do
        namedFunctions
        |> List.iter (fun nf ->
            blank()
            write 0 "void %s_%s__wrapper(int serverID) {" name nf.Name
            write 1 "auto inst = Server%s::getByID(serverID);" name
            for argName, argType in nf.Args do
                popValue writer 1 argType (Variable argName)
            let invocation =
                sprintf "inst->%s(%s)" nf.Name (joinedArgNames nf.Args)
            match nf.Throws with
            | Some (ExceptionType exceptionName) ->
                write 1 "try {"
                pushValue writer 2 nf.Returns invocation true
                write 1 "}"
                write 1 "catch (const %s& e) {" exceptionName
                write 2 "ni_setException(%s);" (lowerFirst exceptionName)
                write 2 "%s__push();" exceptionName
                write 1 "}"
            | _ -> // note we're not handling Some (non-exception types)! need to be errored out on type checking ...
                pushValue writer 1 nf.Returns invocation true
            write 0 "}")

    // globals/constants func
    if globals.Length > 0 then
        blank()
        write 0 "void __constantsFunc() {"
        // push in reverse order
        for globalName, globalType in globals |> List.rev do
            pushValue writer 1 globalType globalName false // hmm for isReturn? I guess not because the variable is global and not going anywhere, so it doesn't need to be in the safe area
        write 0 "}"

    // library entry point / registrations
    blank()
    write 0 "extern \"C\" int nativeLibraryInit() {"
    write 1 "auto m = ni_registerModule(\"%s\");" m.Name
    if globals.Length > 0 then
        write 1 "ni_registerModuleConstants(m, &__constantsFunc);"
    // module methods
    for namedFunction in moduleMethods do
        write 1 "ni_registerModuleMethod(m, \"%s\", &%s__wrapper);" namedFunction.Name namedFunction.Name
    // interfaces + methods
    for name, methods in interfaces do
        let lowerName = lowerFirst name
        write 1 "auto %s = ni_registerInterface(m, \"%s\");" lowerName name
        for method in methods do
            write 1 "%s_%s = ni_registerInterfaceMethod(%s, \"%s\", &%s_%s__wrapper);" lowerName method.Name lowerName method.Name name method.Name
    // exceptions
    for name, _ in exceptions do
        let lowerName = lowerFirst name
        write 1 "%s = ni_registerException(m, \"%s\", &%s__buildAndThrow);" lowerName name name
    // done
    write 1 "return 0; // = OK"
    write 0 "}"
    write 0 ""
    // shutdown
    write 0 "extern \"C\" void nativeLibraryShutdown() {"
    // nothing for C++
    write 0 "}"

let renderHeaderException (writer: TextWriter) (exName: string) (exFields: NamedField list) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    write 0 "class %s {" exName
    write 0 "public:"
    for namedField in exFields do
        write 1 "%s %s;" (typeToString namedField.Type_) namedField.Name
    let joinedInitializers =
        [for nf in exFields -> sprintf "%s(%s)" nf.Name nf.Name]
        |> String.concat ", "
    write 1 "%s(%s) : %s {}" exName (joinedFields exFields) joinedInitializers
    write 0 "};"

let renderHeaderInterface (writer: TextWriter) (name: string) (methods: NamedFunction list) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    write 0 "class %s {" name
    write 0 "public:"
    for namedFunc in methods do
        let throwsComment =
            match namedFunc.Throws with
            | Some exceptionType -> sprintf "  // throws %s" (typeToString exceptionType)
            | None -> ""
        write 1 "virtual %s %s(%s) = 0;%s" (typeToString namedFunc.Returns) namedFunc.Name (joinedArgs namedFunc.Args) throwsComment
    write 0 "};"
    write 0 ""
    // server wrapper
    let nameRegex = Regex(@"%%IFACE%%")
    writer.WriteLine(nameRegex.Replace(ServerWrapperTemplate, name))

let renderHeaderUserOrGeneric (writer: TextWriter) (name: string) (content: UserOrGeneric) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""
    blank()
    match content with
    | UserOrGeneric.Generic gType ->
        match gType with
        | ArrayType _ ->
            write 0 "// %s" (typeToString gType) // remove this eventually
        | MapType _ ->
            write 0 "// %s" (typeToString gType) // likewise
        | _ ->
            failwithf "renderUserTypes: unsupported generic type %A" gType
    | UserOrGeneric.User userType ->
        match userType with
        | OpaqueHandleDef ->
            write 0 "struct __%s; typedef struct __%s* %s;" name name name
        | StructDef namedFields ->
            write 0 "typedef struct {"
            for field in namedFields do
                write 1 "%s %s;" (typeToString field.Type_) field.Name
            write 0 "} %s;" name
        | ExceptionDef namedFields ->
            renderHeaderException writer name namedFields
        | InterfaceDef namedFunctions ->
            renderHeaderInterface writer name namedFunctions
        | DelegateDef info ->
            write 0 "typedef %s %s(%s);" (typeToString info.Returns) name (joinedArgs info.Args)
        | TypeDef actual ->
            write 0 "typedef %s %s;" (typeToString actual) name

let generateImplementationH (m: Module) (writer: TextWriter) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""
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
    write 0 "#include \"NativeImplServer.h\""
    write 0 "#include <functional>"
    write 0 "#include <memory>"
    write 0 "#include <string>"
    write 0 "#include <vector>"
    write 0 "#include <map>"

    // all user types in dependency order
    renderUserTypes writer (userTypesAndGenerics m) renderHeaderUserOrGeneric

    // global externs
    if globals.Length > 0 then
        blank()
        for globalName, globalType in globals do
            write 0 "extern const %s %s;" (typeToString globalType) globalName

    // function prototypes
    blank()
    for namedFunc in moduleMethods do
        let throwsComment =
            match namedFunc.Throws with
            | Some exceptionType -> sprintf "  // throws %s" (typeToString exceptionType)
            | None -> ""
        write 0 "%s %s(%s);%s" (typeToString namedFunc.Returns) namedFunc.Name (joinedArgs namedFunc.Args) throwsComment

let generateImplementationCpp (m: Module) (writer: TextWriter) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""
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
    write 0 "#include \"Implementation.h\""
    // globals
    if globals.Length > 0 then
        blank()
        for globalName, globalType in globals do
            write 0 "const %s %s = %s;" (typeToString globalType) globalName (defaultValueForType globalType)
    // functions
    for namedFunc in moduleMethods do
        blank()
        let throwsComment =
            match namedFunc.Throws with
            | Some exceptionType -> sprintf "  // throws %s" (typeToString exceptionType)
            | None -> ""
        write 0 "%s %s(%s) {%s" (typeToString namedFunc.Returns) namedFunc.Name (joinedArgs namedFunc.Args) throwsComment
        if namedFunc.Returns <> UnitType then
            write 1 "return %s;" (defaultValueForType namedFunc.Returns)
        write 0 "}"

let generate (m: Module) =
    printfn "C++ writing Wrappers.cpp"
    use wrappersCpp = new StreamWriter(@"F:\Workbench\nativeimpl\server\cpp_generated\TestLibraryCpp\Wrappers.cpp")
    generateWrappersCpp m wrappersCpp

    printfn "C++ writing Implementation.h"
    use implementationH = new StreamWriter(@"F:\Workbench\nativeimpl\server\cpp_generated\TestLibraryCpp\Implementation.h")
    generateImplementationH m implementationH

    printfn "C++ writing Implementation.cpp"
    use implementationCpp = new StreamWriter(@"F:\Workbench\nativeimpl\server\cpp_generated\TestLibraryCpp\Implementation.cpp")
    generateImplementationCpp m implementationCpp
