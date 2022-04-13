[<RequireQualifiedAccess>]
module CppGenerator

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

let typeToPopFunc = function
    | Bool -> "ni_popBool"
    | Int32 -> "ni_popInt"
    | InterfaceType name -> "%s__pop"
    | AnonymousFunction _ -> "ni_popClientFunc"
    | _ -> "(not done yet)"

let pushArg (writer: TextWriter) (level: int) (name: string) (type_: NIType) =
    let indent = String.replicate level "    "
    match type_ with
    | Int32 -> fprintfn writer "%sni_pushInt(%s);" indent name
    | Bool -> fprintfn writer "%sni_pushBool(%s);" indent name
    | _ -> fprintfn writer "%sni_push???;" indent

let rec popArg (writer: TextWriter) (level: int) (name: string) (type_: NIType) =
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
        |> List.iteri (fun i type_ -> pushArg writer 2 (sprintf "_%d" i) type_)
        fprintfn writer "%s    %s__cf->remoteExec();" indent name
        popArg writer (level + 1) "__ret" retType
        fprintfn writer "%s    return __ret;" indent
        fprintfn writer "%s};" indent
    | _ ->
        fprintfn writer "%s(not yet implemented);" indent

let pushValue (writer: TextWriter) (level: int) (type_: NIType) (content: string) =
    let indent = String.replicate level "    "
    match type_ with
    | UnitType ->
        fprintfn writer "%s%s;" indent content // no push!
    | Bool ->
        fprintfn writer "%sni_pushBool(%s);" indent content
    | Int32 ->
        fprintfn writer "%sni_pushInt(%s);" indent content
    | InterfaceType name ->
        fprintfn writer "%s%s__push(%s, true);" indent name content
    | AnonymousFunction (niTypes, niType) ->
        fprintfn writer "%sauto __f = %s;" indent content
        fprintfn writer "%ssize_t __uniqueKey = 0;" indent // todo: block below
        fprintfn writer "%sauto __wrapper = [__f]() {" indent
        // pop args
        niTypes
        |> List.iteri (fun i argType ->
            popArg writer (level + 1) (sprintf "_%d" i) argType)
        // push result (func application)
        fprintfn writer "%s};" indent
        fprintfn writer "%spushServerFuncVal(__wrapper, __uniqueKey);" indent
    | _ ->
        fprintfn writer "%s(pushValue not supported yet)" indent

let generate (m: Module) (writer: TextWriter) =
    // opaques + interface method + exception handles
    m.Defs
    |> List.iter (fun def ->
        match def with
        | UserType (name, userType) ->
            match userType with
            | OpaqueHandleDef ->
                fprintfn writer "NIHANDLE(%s);" name
            | InterfaceDef namedFunctions ->
                namedFunctions
                |> List.iter (fun nf ->
                    fprintfn writer "ni_InterfaceMethodRef %s_%s;" name nf.Name)
            | ExceptionDef fieldTypes ->
                fprintfn writer "ni_ExceptionRef %s;" name
            | _ ->
                ()
        | _ ->
            ()
                ) // end defs iter

    // interface push/pops
    m.Defs
    |> List.iter (fun def ->
        match def with
        | UserType(name, InterfaceDef namedFunctions) ->
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
                popArg writer 1 argName argType
            let argsJoined =
                [for name, _ in namedFunction.Args -> name]
                |> String.concat ", "
            pushValue writer 1 namedFunction.Returns (sprintf "%s(%s)" namedFunction.Name argsJoined)
            fprintfn writer "}"
            fprintfn writer ""
        | _ ->
            ()
                ) // end plain function wrappers
