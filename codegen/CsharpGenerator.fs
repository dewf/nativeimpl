[<RequireQualifiedAccess>]
module CsharpGenerator

open System.IO
open System.Text.RegularExpressions
open Parser

open Util

let interfacePopTemplate = "        private static %%IFACE%% %%IFACE%%__Pop()
        {
            NativeImplClient.PopInstanceId(out var id, out var isClientId);
            if (!isClientId)
            {
                return new Server_%%IFACE%%(id);
            }
            else
            {
                return (%%IFACE%%) NativeImplClient.ClientObject.GetById(id);
            }
        }"

let rec typeToString = function
    | UnitType -> "void"
    | Bool -> "bool"
    | Int32 -> "int"
    | StringType -> "string"
    | InterfaceType name -> name
    | ExceptionType name -> name
    | DelegateType name -> name
    | ArrayType niType -> sprintf "%s[]" (typeToString niType)
    | _ -> "(typeToString: not yet supported)"

type PopValueContext =
    | NewVariable of name: string
    | ExistingVar of name: string
    | Return

let argNamesJoined (args: (string * NIType) list) =
    [for name, _ in args -> name]
    |> String.concat ", "

let argsJoined (args: (string * NIType) list) =
    [for name, type_ in args -> sprintf "%s %s" (typeToString type_) name]
    |> String.concat ", "

let fieldsJoined (fields: NamedField list) =
    [for f in fields -> sprintf "%s %s" (typeToString f.Type_) f.Name]
    |> String.concat ", "

let rec popValue (writer: TextWriter) (level: int) (type_: NIType) (context: PopValueContext) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    match type_ with
    | UnitType ->
        () // nothin'
    | Bool ->
        match context with
        | NewVariable name ->
            write level "var %s = NativeImplClient.PopBool();" name
        | ExistingVar name ->
            write level "%s = NativeImplClient.PopBool();" name
        | Return ->
            write level "return NativeImplClient.PopInt();"
    | Int32 ->
        match context with
        | NewVariable name ->
            write level "var %s = NativeImplClient.PopInt();" name
        | ExistingVar name ->
            write level "%s = NativeImplClient.PopInt();" name
        | Return ->
            write level "return NativeImplClient.PopInt();"
    | StringType ->
        match context with
        | NewVariable name ->
            write level "var %s = NativeImplClient.PopString();" name
        | ExistingVar name ->
            write level "%s = NativeImplClient.PopString();" name
        | Return ->
            write level "return NativeImplClient.PopString();"
    | InterfaceType ifaceName ->
        match context with
        | NewVariable name ->
            write level "var %s = %s__Pop();" name ifaceName
        | ExistingVar name ->
            write level "%s = %s__Pop();" name ifaceName
        | Return ->
            // 'Return' means we're returning from our own thing, but does that always coincide with a return right after a remote function execution?
            // ie, would we ever need to clear the safety area in a variable context above?
            write level "var __ret = %s__Pop();" ifaceName
            write level "NativeImplClient.ServerClearSafetyArea();"
            write level "return __ret;"
    | DelegateType name ->
        match context with
        | NewVariable name ->
            write level "var %s = %s__Pop();" name name
        | ExistingVar name ->
            write level "%s = %s__Pop();" name name
        | Return ->
            write level "return %s__Pop();" name
    | ArrayType niType ->
        let popFunc =
            match niType with
            | Bool -> "PopBoolArray"
            | Int32 -> "PopIntArray"
            | StringType -> "PopStringArray"
            | _ -> "(unsupported)"
        match context with
        | NewVariable name ->
            write level "var %s = NativeImplClient.%s();" name popFunc
        | ExistingVar name ->
            write level "%s = NativeImplClient.%s();" name popFunc
        | Return ->
            write level "return NativeImplClient.%s();" popFunc
    | _ ->
        write level "(popValue %A not yet supported)" type_
and pushValue (writer: TextWriter) (level: int) (type_: NIType) (content: string) (isReturn: bool) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    match type_ with
    | UnitType ->
        write level "%s;" content // content directly (method invocation etc)
    | Bool ->
        write level "NativeImplClient.PushBool(%s);" content
    | Int32 ->
        write level "NativeImplClient.PushInt(%s);" content
    | StringType ->
        write level "NativeImplClient.PushString(%s);" content
    | InterfaceType _ ->
        write level "((NativeImplClient.IPushable)%s).Push(%s);" content (if isReturn then "true" else "false")
    | DelegateType name ->
        write level "%s__Push(%s);" name content
    | ArrayType niType ->
        let pushFunc =
            match niType with
            | Bool -> "PushBoolArray"
            | Int32 -> "PushIntArray"
            | StringType -> "PushStringArray"
            | _ -> "(not yet supported)"
        write level "NativeImplClient.%s(%s);" pushFunc content
    | _ ->
        write level "(pushValue %A not yet supported)" type_

let writeLibrary (m: Module) (writer: TextWriter) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""

    let moduleMethods =
        m.Defs
        |> List.choose (function
            | FunctionDef namedFunction -> Some namedFunction
            | _ -> None)
    let delegates =
        m.Defs
        |> List.choose (function
            | UserType(name, DelegateDef delegateInfo) -> Some (name, delegateInfo)
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
    let globals =
        m.Defs
        |> List.choose (function
            | Global(name, niType) -> Some (name, niType)
            | _ -> None)

    // handles for methods etc
    write 0 "using System;"
    write 0 "using System.Diagnostics;"
    write 0 "using System.Runtime.InteropServices;"
    write 0 ""
    write 0 "namespace %sClient" m.Name
    write 0 "{"
    write 1 "public static class %s" m.Name
    write 1 "{"
    write 2 "private static ModuleHandle _module;"

    // top level functions
    for namedFunc in moduleMethods do
        write 2 "private static ModuleMethodHandle _%s;" namedFunc.Name
    blank()

    // interfaces
    for name, methods in interfaces do
        write 2 "private static InterfaceHandle _%s;" name
        for method in methods do
            write 2 "private static InterfaceMethodHandle _%s_%s;" name method.Name
        blank()

    // exceptions
    for name, _ in exceptions do
        write 2 "private static ExceptionHandle _%s;" (lowerFirst name)
    blank()

    // globals declaration
    for name, type_ in globals do
        write 2 "public static %s %s { get; private set; }" (typeToString type_) (upperFirst name)
    blank()

    // static init ===================
    write 2 "public static void Init()"
    write 2 "{"
    write 3 "Debug.Assert(NativeImplClient.Init() == 0);"
    blank()
    write 3 "_module = NativeImplClient.GetModule(\"%s\");" m.Name
    blank()

    // fetch globals
    if globals.Length > 0 then
        write 3 "NativeImplClient.PushModuleConstants(_module);"
        for name, type_ in globals do
            popValue writer 3 type_ (ExistingVar (upperFirst name))
        blank()

    // module method handles
    for namedFunc in moduleMethods do
        write 3 "_%s = NativeImplClient.GetModuleMethod(_module, \"%s\");" namedFunc.Name namedFunc.Name
    blank()

    // exceptions
    for name, _ in exceptions do
        write 3 "_%s = NativeImplClient.GetException(_module, \"%s\");" (lowerFirst name) name
        write 3 "NativeImplClient.SetExceptionBuilder(_%s, %s.Pop);" (lowerFirst name) name
        blank()

    // interface method handles, wrappers
    for ifaceName, methods in interfaces do
        write 3 "_%s = NativeImplClient.GetInterface(_module, \"%s\");" ifaceName ifaceName
        // methods
        for method in methods do
            write 3 "_%s_%s = NativeImplClient.GetInterfaceMethod(_%s, \"%s\");" ifaceName method.Name ifaceName method.Name
        // method wrappers
        for method in methods do
            blank()
            write 3 "NativeImplClient.SetClientMethodWrapper(_%s_%s, delegate(NativeImplClient.ClientObject obj)" ifaceName method.Name
            write 3 "{"
            write 4 "var inst = (Client_%s) obj;" ifaceName
            // pop args
            for argName, argType in method.Args do
                popValue writer 4 argType (NewVariable argName)
            // exec + return
            let invocation =
                sprintf "inst.%s(%s)" (upperFirst method.Name) (argNamesJoined method.Args)
            match method.Throws with
            | Some eType ->
                write 4 "try"
                write 4 "{"
                pushValue writer 5 method.Returns invocation true
                write 4 "}"
                write 4 "catch (%s e)" (typeToString eType)
                write 4 "{"
                write 5 "NativeImplClient.SetException(_%s);" (lowerFirst (typeToString eType))
                write 5 "e.Push();"
                write 4 "}"
            | None ->
                pushValue writer 4 method.Returns invocation true
            write 3 "});"
    write 2 "}" // end of Init()
    blank()
    write 2 "public static void Shutdown()"
    write 2 "{"
    write 3 "NativeImplClient.Shutdown();"
    write 2 "}"

    // delegate declarations + push/pop
    for name, info in delegates do
        blank()
        write 2 "public delegate %s %s(%s);" (typeToString info.Returns) name (argsJoined info.Args)
        // push
        blank()
        write 2 "private static void %s__Push(%s callback)" name name
        write 2 "{"
        write 3 "void CallbackWrapper()"
        write 3 "{"
        for argName, argType in info.Args do
            popValue writer 4 argType (NewVariable argName)
        let invocation =
            sprintf "callback(%s)" (argNamesJoined info.Args)
        pushValue writer 4 info.Returns invocation true
        write 3 "}"
        write 3 "NativeImplClient.PushClientFuncVal(CallbackWrapper, Marshal.GetFunctionPointerForDelegate(callback));"
        write 2 "}"
        // pop
        blank()
        write 2 "private static %s %s__Pop()" name name
        write 2 "{"
        write 3 "var id = NativeImplClient.PopServerFuncValId();"
        write 3 "var remoteFunc = new NativeImplClient.ServerFunc(id);"
        write 3 "%s Wrapper(%s)" (typeToString info.Returns) (argsJoined info.Args)
        write 3 "{"
        for argName, argType in info.Args |> List.rev do
            pushValue writer 4 argType argName false
        write 4 "remoteFunc.Exec();"
        popValue writer 4 info.Returns Return
        write 3 "}"
        write 3 "return Wrapper;"
        write 2 "}"

    // top-level module method wrappers
    for namedFunc in moduleMethods do
        blank()
        write 2 "public static %s %s(%s)" (typeToString namedFunc.Returns) (upperFirst namedFunc.Name) (argsJoined namedFunc.Args)
        write 2 "{"
        // push args in reverse
        for argName, argType in namedFunc.Args |> List.rev do
            pushValue writer 3 argType argName false
        // invoke remote
        match namedFunc.Throws with
        | Some _ ->
            write 3 "NativeImplClient.InvokeModuleMethodWithExceptions(_%s);" namedFunc.Name
        | None ->
            write 3 "NativeImplClient.InvokeModuleMethod(_%s);" namedFunc.Name
        // return pop
        popValue writer 3 namedFunc.Returns Return
        write 2 "}"

    // interface stuff
    for ifaceName, methods in interfaces do
        blank()
        // raw interface itself
        write 2 "public interface %s : IDisposable" ifaceName
        write 2 "{"
        for method in methods do
            let throwsComment =
                match method.Throws with
                | Some eType -> sprintf " // throws %s" (typeToString eType)
                | None -> ""
            write 3 "%s %s(%s);%s" (typeToString method.Returns) (upperFirst method.Name) (argsJoined method.Args) throwsComment
        write 2 "}"
        // pop template
        blank()
        writer.WriteLine(Regex(@"%%IFACE%%").Replace(interfacePopTemplate, ifaceName))
        // client base class
        blank()
        write 2 "public abstract class Client_%s : NativeImplClient.ClientObject, %s" ifaceName ifaceName
        write 2 "{"
        write 3 "public virtual void Dispose()"
        write 3 "{"
        write 4 "// override if necessary"
        write 3 "}"
        // abstract methods for client base class
        for method in methods do
            let throwsComment =
                match method.Throws with
                | Some eType -> sprintf " // throws %s" (typeToString eType)
                | None -> ""
            write 3 "public abstract %s %s(%s);%s" (typeToString method.Returns) (upperFirst method.Name) (argsJoined method.Args) throwsComment
        write 2 "}"
        // server wrapper
        blank()
        write 2 "private class Server_%s : NativeImplClient.ServerObject, %s" ifaceName ifaceName
        write 2 "{"
        write 3 "public Server_%s(int id) : base(id)" ifaceName
        write 3 "{"
        write 3 "}"
        // server wrapper methods
        for method in methods do
            blank()
            write 3 "public %s %s(%s)" (typeToString method.Returns) (upperFirst method.Name) (argsJoined method.Args)
            write 3 "{"
            // push args
            for argName, argType in method.Args do
                pushValue writer 4 argType argName false
            // invoke remote
            match method.Throws with
            | Some _ ->
                write 4 "NativeImplClient.InvokeInterfaceMethodWithExceptions(_%s_%s, Id);" ifaceName method.Name
            | None ->
                write 4 "NativeImplClient.InvokeInterfaceMethod(_%s_%s, Id);" ifaceName method.Name
            // return
            popValue writer 4 method.Returns Return
            write 3 "}"
        blank()
        write 3 "public void Dispose()"
        write 3 "{"
        write 4 "ServerDispose();"
        write 3 "}"
        write 2 "}"

    // exception bodies
    for name, fields in exceptions do
        blank()
        write 2 "public class %s : Exception" name
        write 2 "{"
        for f in fields do
            write 3 "public readonly %s %s;" (typeToString f.Type_) (upperFirst f.Name)
        write 3 "public %s(%s) : base(\"%s\")" name (fieldsJoined fields) name
        write 3 "{"
        for f in fields do
            write 4 "%s = %s;" (upperFirst f.Name) f.Name
        write 3 "}"
        write 3 "public void Push()"
        write 3 "{"
        for f in fields |> List.rev do
            pushValue writer 4 f.Type_ (upperFirst f.Name) false // isReturn = false since we're still holding a reference to whatever it is (not a return value from a function)
        write 3 "}"
        write 3 "public static %s Pop()" name
        write 3 "{"
        for f in fields do
            popValue writer 4 f.Type_ (NewVariable f.Name)
        let namesJoined =
            [for f in fields -> f.Name] |> String.concat ", "
        write 4 "return new %s(%s);" name namesJoined
        write 3 "}"
        write 2 "}"

    write 1 "}" // end of static class <module name>
    //
    write 0 "}" // end of namespace

let generate (m: Module) =
    printfn "C# writing TestLibrary.cs"
    use testLibraryCs = new StreamWriter(@"F:\Workbench\nativeimpl\client\csharp\testproject\TestLibraryClient\TestLibrary.cs")
    writeLibrary m testLibraryCs
