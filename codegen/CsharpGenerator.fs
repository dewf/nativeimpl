module CsharpGenerator

open System.IO
open System.Text.RegularExpressions
open Parser

open Util
open Common

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

let rec typeToCsString = function
    | UnitType -> "void"
    | Bool -> "bool"
    | Int32 -> "int"
    | StringType -> "string"
    | EnumType name -> upperFirst name
    | StructType name -> upperFirst name
    | InterfaceType name -> upperFirst name
    | ExceptionType name -> upperFirst name
    | DelegateType name -> upperFirst name
    | ArrayType niType -> sprintf "%s[]" (typeToCsString niType)
    | _ -> "(typeToString: not yet supported)"

let rec typeToName = function
    | UnitType -> failwith "typeToName: 'unit' type as name?"
    | Bool -> "Bool"
    | Int32 -> "Int"
    | StringType -> "String"
    | StructType name -> (upperFirst name)
    | EnumType name -> (upperFirst name)
    | _ -> "(typeToName: not yet supported)"

let argNamesJoined (args: (string * NIType) list) =
    [for name, _ in args -> name]
    |> String.concat ", "

let argsJoined (args: (string * NIType) list) =
    [for name, type_ in args -> sprintf "%s %s" (typeToCsString type_) name]
    |> String.concat ", "

let fieldsJoined (fields: NamedField list) =
    [for f in fields -> sprintf "%s %s" (typeToCsString f.Type_) f.Name]
    |> String.concat ", "

let fieldNamesJoined (fields: NamedField list) =
    [for f in fields -> f.Name]
    |> String.concat ", "

type PopValueContext =
    | NewVariable of name: string
    | ExistingVar of name: string
    | Return

let popValue (writer: TextWriter) (level: int) (type_: NIType) (context: PopValueContext) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let contextPrefix =
        match context with
        | NewVariable name -> sprintf "var %s =" name
        | ExistingVar name -> sprintf "%s =" name
        | Return -> "return"
    match type_ with
    | UnitType ->
        () // nothin'
    | Bool ->
        write level "%s NativeImplClient.PopBool();" contextPrefix
    | Int32 ->
        write level "%s NativeImplClient.PopInt();" contextPrefix
    | StringType ->
        write level "%s NativeImplClient.PopString();" contextPrefix
    | InterfaceType ifaceName ->
        write level "%s %s__Pop();" contextPrefix (upperFirst ifaceName)
    | DelegateType name ->
        write level "%s %s__Pop();" contextPrefix (upperFirst name)
    | ArrayType niType ->
        match niType with
        | Bool -> write level "%s NativeImplClient.PopBoolArray();" contextPrefix
        | Int32 -> write level "%s NativeImplClient.PopIntArray();" contextPrefix
        | StringType -> write level "%s NativeImplClient.PopStringArray();" contextPrefix
        | EnumType name -> write level "%s %sArray__Pop();" contextPrefix (upperFirst name)
        | StructType name -> write level "%s %sArray__Pop();" contextPrefix (upperFirst name)
        | _ -> write level "// popValue not yet supported: array of %A" niType
    | StructType name ->
        write level "%s %s__Pop();" contextPrefix (upperFirst name)
    | EnumType name ->
        write level "%s %s__Pop();" contextPrefix (upperFirst name)
    | _ ->
        write level "(popValue %A not yet supported)" type_

type PushIsReturn =
    | Direct of value: bool
    | Indirect of name: string
    | NotApplicable

let pushValue (writer: TextWriter) (level: int) (type_: NIType) (content: string) (isReturn: PushIsReturn) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let isReturnString() =
        match isReturn with
        | Direct value -> if value then "true" else "false"
        | Indirect name -> name
        | NotApplicable -> failwith "pushValue: 'NotApplicable' isReturn was consumed"
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
        write level "((NativeImplClient.IPushable)%s).Push(%s);" content (isReturnString())
    | DelegateType name ->
        write level "%s__Push(%s);" name content
    | ArrayType niType ->
        match niType with
        | Bool -> write level "NativeImplClient.PushBoolArray(%s);" content
        | Int32 -> write level "NativeImplClient.PushIntArray(%s);" content
        | StringType -> write level "NativeImplClient.PushStringArray(%s);" content
        | EnumType name -> write level "%sArray__Push(%s);" (upperFirst name) content
        | StructType name -> write level "%sArray__Push(%s);" (upperFirst name) content
        | _ -> write level "// pushValue not yet supported: Array of %A" niType
    | EnumType name ->
        write level "%s__Push(%s);" name content
    | StructType name ->
        write level "%s__Push(%s, %s);" name content (isReturnString())
    | _ ->
        write level "(pushValue %A not yet supported)" type_

let renderLibraryUserTypes (writer: TextWriter) (containsRefs: NIType -> bool) (name: string) (content: UserOrGeneric) (typeMap: Map<string, UserOrGeneric>) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""
    //
    blank()
    match content with
    | UserOrGeneric.FwdInterface ->
        failwith "renderLibraryUserTypes: shouldn't see this (FwdInterface)"
    | UserOrGeneric.Generic gType ->
        match gType with
        | ArrayType arrayType ->
//            let arrayCommon (name: string) (elementType: NIType) =
//                // use this for any types with no particular specializations (typedefs, delegates, arrays of arrays -- everything but structs and tuples, basically)
//                // PUSH ========
//                write 0 "void __%sArray__push(%s[] values, bool isReturn) {" name (typeToCsString elementType)
//                write 1 "for (auto v = values.rbegin(); v != values.rend(); v++) {" // in reverse!
//                pushValue writer 2 elementType "*v" (Indirect "isReturn")
//                write 1 "}"
//                write 1 "ni_pushSizeT(values.size());"
//                write 0 "}"
//                // POP =========
//                blank()
//                write 0 "std::vector<%s> %sArray__pop() {" (typeToCppString elementType) name
//                write 1 "std::vector<%s> __ret;" (typeToCppString elementType)
//                write 1 "auto count = ni_popSizeT();"
//                write 1 "for (auto i = 0; i < count; i++) {"
//                popValue writer 2 elementType (Variable "value")
//                write 2 "__ret.push_back(value);"
//                write 1 "}"
//                write 1 "return __ret;"
//                write 0 "}"
            match arrayType with
            | EnumType name ->
                // push
                write 2 "private static void %sArray__Push(IEnumerable<%s> items)" (upperFirst name) (upperFirst name)
                write 2 "{"
                write 3 "var intValues = items.Select(i => (int)i).ToArray();"
                write 3 "NativeImplClient.PushIntArray(intValues);"
                write 2 "}"
                // pop
                blank()
                write 2 "private static %s[] %sArray__Pop()" (upperFirst name) (upperFirst name)
                write 2 "{"
                write 3 "var intValues = NativeImplClient.PopIntArray();"
                write 3 "return intValues.Select(i => (%s)i).ToArray();" (upperFirst name)
                write 2 "}"
            | StructType structName ->
                // fetch the fields so we can make a struct-of-arrays
                match typeMap[structName] with
                | UserOrGeneric.User (StructDef namedFields) ->
                    // push
                    write 2 "private static void %sArray__Push(%s[] items)" (upperFirst structName) (upperFirst structName)
                    write 2 "{"
                    write 3 "var count = items.Length;"
                    for i, field in namedFields |> List.indexed do
                        write 3 "var f%dValues = new %s[count];" i (typeToCsString field.Type_)
                    write 3 "for (var i = 0; i < count; i++)"
                    write 3 "{"
                    for i, field in namedFields |> List.indexed do
                        write 4 "f%dValues[i] = items[i].%s;" i (upperFirst field.Name)
                    write 3 "}"
                    // push in reverse order
                    for i, field in namedFields |> List.indexed |> List.rev do
                        pushValue writer 3 (ArrayType field.Type_) (sprintf "f%dValues" i) (Indirect "isReturn")
                    write 2 "}"
                    // pop
                    blank()
                    write 2 "private static %s[] %sArray__Pop()" (upperFirst structName) (upperFirst structName)
                    write 2 "{"
                    for i, field in namedFields |> List.indexed do
                        popValue writer 3 (ArrayType field.Type_) (NewVariable (sprintf "f%dValues" i))
                    write 3 "var count = f1Values.Length;"
                    write 3 "var ret = new %s[count];" (upperFirst structName)
                    write 3 "for (var i = 0; i < count; i++)"
                    write 3 "{"
                    for i, _ in namedFields |> List.indexed do
                        write 4 "var f%d = f%dValues[i];" i i
                    let fieldVarsJoined =
                        namedFields |> List.indexed |> List.map (fun (i, _) -> sprintf "f%d" i) |> String.concat ", "
                    write 4 "ret[i] = new %s(%s);" (upperFirst structName) fieldVarsJoined
                    write 3 "}"
                    write 3 "return ret;"
                    write 2 "}"
                | _ ->
                    failwithf "renderWrapperUserOrGeneric: failed to find struct in typemap"
            | _ ->
                // we don't handle any of the primitives, those are all built-in
                write 2 "// push/pop for generic %s" (typeToCsString gType)
        | _ ->
            write 2 "// %s" (typeToCsString gType)
    | UserOrGeneric.User userType ->
        match userType with
        | OpaqueHandleDef ->
            write 2 "// anything for opaque here? push/pop perhaps? %s" name
        | StructDef namedFields ->
            write 2 "public struct %s {" name
            for field in namedFields do
                write 3 "public %s %s;" (typeToCsString field.Type_) (upperFirst field.Name)
            blank()
            // constructor
            write 3 "public %s(%s)" name (fieldsJoined namedFields)
            write 3 "{"
            for field in namedFields do
                write 4 "this.%s = %s;" (upperFirst field.Name) field.Name
            write 3 "}"
            write 2 "}"
            // push
            blank()
            write 2 "private static void %s__Push(%s value, bool isReturn)" (upperFirst name) (upperFirst name)
            write 2 "{"
            for field in namedFields |> List.rev do
                pushValue writer 3 field.Type_ (sprintf "value.%s" (upperFirst field.Name)) (Indirect "isReturn")
            write 2 "}"
            // pop
            blank()
            write 2 "private static %s %s__Pop()" (upperFirst name) (upperFirst name)
            write 2 "{"
            for field in namedFields do
                popValue writer 3 field.Type_ (NewVariable field.Name)
            write 3 "return new %s(%s);" name (fieldNamesJoined namedFields)
            write 2 "}"
        | EnumDef values ->
            write 2 "public enum %s" name
            write 2 "{"
            for value, maybeComma in values |> withTrailingCommas do
                write 3 "%s%s" (upperFirst value) maybeComma
            write 2 "}"
            blank()
            write 2 "[MethodImpl(MethodImplOptions.AggressiveInlining)]"
            write 2 "private static void %s__Push(%s value)" (upperFirst name) (upperFirst name)
            write 2 "{"
            write 3 "NativeImplClient.PushInt((int)value);"
            write 2 "}"
            blank()
            write 2 "[MethodImpl(MethodImplOptions.AggressiveInlining)]"
            write 2 "private static %s %s__Pop()" (upperFirst name) (upperFirst name)
            write 2 "{"
            write 3 "return (%s)NativeImplClient.PopInt();" (upperFirst name)
            write 2 "}"
        | _ ->
            write 2 "// %A" userType

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
    write 0 "using System.Runtime.CompilerServices;"
    write 0 "using System.Collections.Generic;"
    write 0 "using System.Linq;"
    //write 0 "using System.Runtime.InteropServices;"
    write 0 ""
    write 0 "namespace %sClient" m.Name
    write 0 "{"
    write 1 "public static class %s" m.Name
    write 1 "{"
    write 2 "private static ModuleHandle _module;"

    // top level functions
    for namedFunc in moduleMethods do
        write 2 "private static ModuleMethodHandle _%s;" namedFunc.Name

    // interfaces
    for name, methods in interfaces do
        write 2 "private static InterfaceHandle _%s;" name
        for method in methods do
            write 2 "private static InterfaceMethodHandle _%s_%s;" name method.Name
        blank()

    // exceptions
    for name, _ in exceptions do
        write 2 "private static ExceptionHandle _%s;" (lowerFirst name)

    // globals declaration
    for name, type_ in globals do
        write 2 "public static %s %s { get; private set; }" (typeToCsString type_) (upperFirst name)

    // user types ====================
    let typeMap =
        userTypesAndGenerics m
    let containsRefsMap =
        createContainsRefsMap typeMap
    let renderFunc =
        let containsRefs (type_: NIType) =
            containsRefsMap.ContainsKey(typeToInternalKey type_)
        renderLibraryUserTypes writer containsRefs
    renderUserTypes typeMap renderFunc

    // top-level functions ===========
    for namedFunc in moduleMethods do
        blank()
        write 2 "public static %s %s(%s)" (typeToCsString namedFunc.Returns) (upperFirst namedFunc.Name) (argsJoined namedFunc.Args)
        write 2 "{"
        // push args in reverse
        for argName, argType in namedFunc.Args |> List.rev do
            pushValue writer 3 argType argName (Direct false)
        // invoke remote
        match namedFunc.Throws with
        | Some _ ->
            write 3 "NativeImplClient.InvokeModuleMethodWithExceptions(_%s);" namedFunc.Name
        | None ->
            write 3 "NativeImplClient.InvokeModuleMethod(_%s);" namedFunc.Name
        // return pop
        popValue writer 3 namedFunc.Returns Return
        // TODO: clear safety area?
        write 2 "}"

    // static init + shutdown ====================================================
    blank()
    write 2 "public static void Init()"
    write 2 "{"
    write 3 "Debug.Assert(NativeImplClient.Init() == 0);"
    write 3 "_module = NativeImplClient.GetModule(\"%s\");" m.Name

    // fetch globals
    if globals.Length > 0 then
        blank()
        write 3 "NativeImplClient.PushModuleConstants(_module);"
        for name, type_ in globals do
            popValue writer 3 type_ (ExistingVar (upperFirst name))

    // module method handles
    if not moduleMethods.IsEmpty then
        blank()
        for namedFunc in moduleMethods do
            write 3 "_%s = NativeImplClient.GetModuleMethod(_module, \"%s\");" namedFunc.Name namedFunc.Name

    // exceptions
    if not exceptions.IsEmpty then
        blank()
        for name, _ in exceptions do
            write 3 "_%s = NativeImplClient.GetException(_module, \"%s\");" (lowerFirst name) name
            write 3 "NativeImplClient.SetExceptionBuilder(_%s, %s.Pop);" (lowerFirst name) name

    // interface method handles, wrappers
    if not interfaces.IsEmpty then
        blank()
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
                    pushValue writer 5 method.Returns invocation (Direct true)
                    write 4 "}"
                    write 4 "catch (%s e)" (typeToCsString eType)
                    write 4 "{"
                    write 5 "NativeImplClient.SetException(_%s);" (lowerFirst (typeToCsString eType))
                    write 5 "e.Push();"
                    write 4 "}"
                | None ->
                    pushValue writer 4 method.Returns invocation (Direct true)
                write 3 "});"

    write 2 "}" // end of Init()
    blank()
    write 2 "public static void Shutdown()"
    write 2 "{"
    write 3 "NativeImplClient.Shutdown();"
    write 2 "}"

//    // delegate declarations + push/pop
//    for name, info in delegates do
//        blank()
//        write 2 "public delegate %s %s(%s);" (typeToCsString info.Returns) name (argsJoined info.Args)
//        // push
//        blank()
//        write 2 "private static void %s__Push(%s callback)" name name
//        write 2 "{"
//        write 3 "void CallbackWrapper()"
//        write 3 "{"
//        for argName, argType in info.Args do
//            popValue writer 4 argType (NewVariable argName)
//        let invocation =
//            sprintf "callback(%s)" (argNamesJoined info.Args)
//        pushValue writer 4 info.Returns invocation true
//        write 3 "}"
//        write 3 "NativeImplClient.PushClientFuncVal(CallbackWrapper, Marshal.GetFunctionPointerForDelegate(callback));"
//        write 2 "}"
//        // pop
//        blank()
//        write 2 "private static %s %s__Pop()" name name
//        write 2 "{"
//        write 3 "var id = NativeImplClient.PopServerFuncValId();"
//        write 3 "var remoteFunc = new NativeImplClient.ServerFunc(id);"
//        write 3 "%s Wrapper(%s)" (typeToCsString info.Returns) (argsJoined info.Args)
//        write 3 "{"
//        for argName, argType in info.Args |> List.rev do
//            pushValue writer 4 argType argName false
//        write 4 "remoteFunc.Exec();"
//        popValue writer 4 info.Returns Return
//        write 3 "}"
//        write 3 "return Wrapper;"
//        write 2 "}"

//    // interface stuff
//    for ifaceName, methods in interfaces do
//        blank()
//        // raw interface itself
//        write 2 "public interface %s : IDisposable" ifaceName
//        write 2 "{"
//        for method in methods do
//            let throwsComment =
//                match method.Throws with
//                | Some eType -> sprintf " // throws %s" (typeToCsString eType)
//                | None -> ""
//            write 3 "%s %s(%s);%s" (typeToCsString method.Returns) (upperFirst method.Name) (argsJoined method.Args) throwsComment
//        write 2 "}"
//        // pop template
//        blank()
//        writer.WriteLine(Regex(@"%%IFACE%%").Replace(interfacePopTemplate, ifaceName))
//        // client base class
//        blank()
//        write 2 "public abstract class Client_%s : NativeImplClient.ClientObject, %s" ifaceName ifaceName
//        write 2 "{"
//        write 3 "public virtual void Dispose()"
//        write 3 "{"
//        write 4 "// override if necessary"
//        write 3 "}"
//        // abstract methods for client base class
//        for method in methods do
//            let throwsComment =
//                match method.Throws with
//                | Some eType -> sprintf " // throws %s" (typeToCsString eType)
//                | None -> ""
//            write 3 "public abstract %s %s(%s);%s" (typeToCsString method.Returns) (upperFirst method.Name) (argsJoined method.Args) throwsComment
//        write 2 "}"
//        // server wrapper
//        blank()
//        write 2 "private class Server_%s : NativeImplClient.ServerObject, %s" ifaceName ifaceName
//        write 2 "{"
//        write 3 "public Server_%s(int id) : base(id)" ifaceName
//        write 3 "{"
//        write 3 "}"
//        // server wrapper methods
//        for method in methods do
//            blank()
//            write 3 "public %s %s(%s)" (typeToCsString method.Returns) (upperFirst method.Name) (argsJoined method.Args)
//            write 3 "{"
//            // push args
//            for argName, argType in method.Args do
//                pushValue writer 4 argType argName false
//            // invoke remote
//            match method.Throws with
//            | Some _ ->
//                write 4 "NativeImplClient.InvokeInterfaceMethodWithExceptions(_%s_%s, Id);" ifaceName method.Name
//            | None ->
//                write 4 "NativeImplClient.InvokeInterfaceMethod(_%s_%s, Id);" ifaceName method.Name
//            // return
//            popValue writer 4 method.Returns Return
//            write 3 "}"
//        blank()
//        write 3 "public void Dispose()"
//        write 3 "{"
//        write 4 "ServerDispose();"
//        write 3 "}"
//        write 2 "}"

//    // exception bodies
//    for name, fields in exceptions do
//        blank()
//        write 2 "public class %s : Exception" name
//        write 2 "{"
//        for f in fields do
//            write 3 "public readonly %s %s;" (typeToCsString f.Type_) (upperFirst f.Name)
//        write 3 "public %s(%s) : base(\"%s\")" name (fieldsJoined fields) name
//        write 3 "{"
//        for f in fields do
//            write 4 "%s = %s;" (upperFirst f.Name) f.Name
//        write 3 "}"
//        write 3 "public void Push()"
//        write 3 "{"
//        for f in fields |> List.rev do
//            pushValue writer 4 f.Type_ (upperFirst f.Name) false // isReturn = false since we're still holding a reference to whatever it is (not a return value from a function)
//        write 3 "}"
//        write 3 "public static %s Pop()" name
//        write 3 "{"
//        for f in fields do
//            popValue writer 4 f.Type_ (NewVariable f.Name)
//        let namesJoined =
//            [for f in fields -> f.Name] |> String.concat ", "
//        write 4 "return new %s(%s);" name namesJoined
//        write 3 "}"
//        write 2 "}"

    write 1 "}" // end of static class <module name>
    //
    write 0 "}" // end of namespace

let generate (m: Module) =
    printfn "C# writing TestLibrary.cs"
    use testLibraryCs = new StreamWriter(@"F:\Workbench\nativeimpl\client\csharp\testproject\TestLibraryClient\TestLibrary.cs")
    writeLibrary m testLibraryCs
