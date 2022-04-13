[<RequireQualifiedAccess>]
module CppGenerator

open System
open System.IO
open System.Text.RegularExpressions
open AbstractSyntaxTree

let PopTemplate = "
std::shared_ptr<%%IFACE%%> %%IFACE%%__pop()
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
let PushTemplate = "
inline void %%IFACE%%__push(std::shared_ptr<%%IFACE%%> inst, bool isReturn) {
    auto pushable = std::dynamic_pointer_cast<Pushable>(inst);
    pushable->push(pushable, isReturn);
}"

let typeToString = function
    | UnitType -> "void"
    | Bool -> "bool"
    | Int32 -> "int"
    | _ -> "(not done yet)"

let pushFunctionTypedef (writer: TextWriter) (level: int) (name: string) (args: NIType list) (ret: NIType) =
    let indent = String.replicate level "    "
    let argsJoined =
        [for arg in args -> typeToString arg]
        |> String.concat ", "
    fprintfn writer "%stypedef %s %s(%s);" indent (typeToString ret) name argsJoined

let rec popVariable (writer: TextWriter) (level: int) (name: string) (type_: NIType) =
    let indent = String.replicate level "    "
    match type_ with
    | Bool ->
        fprintfn writer "%sbool %s = ni_popBool();" indent name
    | Int32 ->
        fprintfn writer "%sint %s = ni_popInt();" indent name
    | InterfaceType ifaceName ->
        fprintfn writer "%s%s %s = %s__pop();" indent ifaceName name ifaceName
    | AnonymousFunction (argTypes, retType) ->
        fprintfn writer "%sauto %s__id = ni_popClientFunc();" indent name
        fprintfn writer "%sauto %s__cf = std::shared_ptr<ClientFunc>(new ClientFunc(%s__id));" indent name name
        let argList =
            argTypes
            |> List.mapi (fun i type_ ->
                sprintf "%s _%d" (typeToString(type_)) i)
            |> String.concat ", "
        fprintfn writer "%sauto %s = [%s__cf](%s) {" indent name name argList
        // push those inner params in reverse order
        argTypes
        |> List.rev
        |> List.iteri (fun i type_ -> pushValue writer (level + 1) type_ (sprintf "_%d" i) false)
        fprintfn writer "%s    %s__cf->remoteExec();" indent name
        popVariable writer (level + 1) "__ret" retType
        fprintfn writer "%s    return __ret;" indent
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
            popVariable writer (level + 1) (sprintf "_%d" i) argType)
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

let generate (m: Module) (writer: TextWriter) =
    // opaques + interface method + exception handles
    m.Defs
    |> List.iter (fun def ->
        match def with
        | UserType (name, userType) ->
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
        | _ ->
            ()
                ) // end defs iter

    // interface push/pops
    m.Defs
    |> List.iter (fun def ->
        match def with
        | UserType(name, InterfaceDef _) ->
            let r = Regex(@"%%IFACE%%")
            writer.WriteLine(r.Replace(PushTemplate, name))
            writer.WriteLine(r.Replace(PopTemplate, name))
        | _ ->
            ()   ) // end iface push/pop iter

    fprintfn writer ""

    // plain function wrappers
    m.Defs
    |> List.iter (function
        | FunctionDef namedFunction ->
            fprintfn writer "void %s__wrapper() {" namedFunction.Name
            for argName, argType in namedFunction.Args do
                popVariable writer 1 argName argType
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
        | _ ->
            ()  ) // end plain function wrappers

    // interface wrappers
    m.Defs
    |> List.iter (function
        | UserType (name, InterfaceDef namedFunctions) ->
            namedFunctions
            |> List.iter (fun nf ->
                fprintfn writer "void %s_%s__wrapper(int serverID) {" name nf.Name
                fprintfn writer "    auto inst = Server%s::getByID(serverID);" name
                for argName, argType in nf.Args do
                    popVariable writer 1 argName argType
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
        | _ ->
            ()  ) // end interface wrappers
