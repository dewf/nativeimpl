[<RequireQualifiedAccess>]
module CsharpGenerator

open System.IO
open Parser

open Util

let rec typeToString = function
    | UnitType -> "void"
    | Int32 -> "int"
    | InterfaceType name -> name
    | ExceptionType name -> name
    | AnonymousFunction (argTypes, returnType) ->
        let argTypesJoined =
            [for t in argTypes -> typeToString t]
            |> String.concat ","
        if returnType = UnitType then
            sprintf "Action<%s>" argTypesJoined
        else
            sprintf "Func<%s,%s>" argTypesJoined (typeToString returnType)
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
    | AnonymousFunction (argTypes, returnType) ->
        let common name =
            write level "var %s__id = NativeImplClient.PopServerFuncValId();" name
            write level "var %s__remoteFunc = new NativeImplClient.ServerFunc(%s__id);" name name
            let joinedArgs =
                [for i, type_ in argTypes |> List.indexed -> sprintf "%s _%d" (typeToString type_) i]
                |> String.concat ", "
            write level "%s %sWrapper(%s)" (typeToString returnType) (upperFirst name) joinedArgs
            write level "{"
            // push args in reverse order
            for i, arg in argTypes |> List.indexed |> List.rev do
                pushValue writer (level + 1) arg (sprintf "_%d" i) false
            // exec
            write (level + 1) "%s__remoteFunc.Exec();" name
            // pop and return
            popValue writer (level + 1) returnType Return
            write level "}"
        match context with
        | NewVariable name ->
            common name
            write level "var %s = %sWrapper;" name (upperFirst name)
        | ExistingVar name ->
            common name
            write level "%s = %sWrapper;" name (upperFirst name)
        | Return ->
            common "__ret"
            write level "return %sWrapper;" "__ret"
    | _ ->
        write level "(popValue %A not yet supported)" type_
and pushValue (writer: TextWriter) (level: int) (type_: NIType) (content: string) (isReturn: bool) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    match type_ with
    | UnitType ->
        write level "%s;" content // content directly (method invocation etc)
    | Int32 ->
        write level "NativeImplClient.PushInt(%s);" content
    | InterfaceType _ ->
        write level "((NativeImplClient.IPushable)%s).Push(%s);" content (if isReturn then "true" else "false")
    | AnonymousFunction (argTypes, returnType) ->
        let wrapperName =
            sprintf "%sWrapper" (upperFirst content) // content = outer function argument name, hopefully!
        write level "void %s()" wrapperName
        write level "{"
        // pop args
        for i, argType in argTypes |> List.indexed do
            popValue writer (level + 1) argType (NewVariable (sprintf "_%d" i))
        // push invocation result
        let invocation =
            let joinedArgs = [for i, _ in argTypes |> List.indexed -> sprintf "_%d" i] |> String.concat ", "
            sprintf "%s(%s)" content joinedArgs
        pushValue writer (level + 1) returnType invocation true
        write level "}"
        write level "NativeImplClient.PushClientFuncVal(%s, Marshal.GetFunctionPointerForDelegate(%s));" wrapperName content
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
    //
    write 1 "}" // end of static class <module name>
    //
    write 0 "}" // end of namespace

let generate (m: Module) =
    printfn "===== TestLibrary.cs =============="
    writeLibrary m stdout
