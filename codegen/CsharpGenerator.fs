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
                return new Server%%IFACE%%(id);
            }
            else
            {
                return (%%IFACE%%) NativeImplClient.ClientObject.GetById(id);
            }
        }"

let rec typeToCsString (typeMap: Map<string, UserOrGeneric>) = function
    | UnitType -> "void"
    | Bool -> "bool"
    | Int32 -> "int"
    | StringType -> "string"
    | EnumType name -> upperFirst name
    | SumType name -> upperFirst name
    | StructType name -> upperFirst name
    | InterfaceType name -> upperFirst name
    | ExceptionType name -> upperFirst name
    | DelegateType name -> upperFirst name
    | ArrayType niType -> sprintf "%s[]" (typeToCsString typeMap niType)
    | TupleType tupleTypes ->
        let joined =
            tupleTypes
            |> List.map (typeToCsString typeMap)
            |> String.concat ", "
        sprintf "(%s)" joined
    | TypedefType name ->
        // no C# typedefs so we just render the type directly
        match typeMap.TryFind(name) with
        | Some (UserOrGeneric.User (TypeDef niType)) ->
            typeToCsString typeMap niType
        | _ ->
            failwithf "typeToCsString: failed to find typedef definition"
    | MapType (keyType, valueType) ->
        sprintf "Dictionary<%s,%s>" (typeToCsString typeMap keyType) (typeToCsString typeMap valueType)
    | x -> sprintf "(typeToCsString: not yet supported: %A)" x

let rec newCsArray (typeMap: Map<string, UserOrGeneric>) (ofType: NIType) (countVar: string) =
    match ofType with
    | ArrayType niType ->
        sprintf "%s[]" (newCsArray typeMap niType countVar)
    | _ ->
        sprintf "new %s[%s]" (typeToCsString typeMap ofType) countVar

let rec typeToName = function
    | UnitType -> failwith "typeToName: 'unit' type as name?"
    | Bool -> "Bool"
    | Int32 -> "Int"
    | StringType -> "String"
    | StructType name -> upperFirst name
    | EnumType name -> upperFirst name
    | TypedefType name -> upperFirst name
    | TupleType tupleTypes ->
        let joined =
            tupleTypes
            |> List.map typeToName
            |> String.concat ""
        sprintf "%sTuple" joined
    | ArrayType niType ->
        sprintf "%sArray" (typeToName niType)
    | MapType (keyType, valueType) ->
        sprintf "%s%sMap" (typeToName keyType) (typeToName valueType)
    | x -> sprintf "(typeToName: not yet supported: %A)" x

let argsJoined (typeMap: Map<string, UserOrGeneric>) (args: (string * NIType) list) =
    [for name, type_ in args -> sprintf "%s %s" (typeToCsString typeMap type_) name]
    |> String.concat ", "

let argNamesJoined (args: (string * NIType) list) =
    [for name, _ in args -> name]
    |> String.concat ", "

let fieldsJoined (typeMap: Map<string, UserOrGeneric>) (fields: NamedField list) =
    [for f in fields -> sprintf "%s %s" (typeToCsString typeMap f.Type_) f.Name]
    |> String.concat ", "

let fieldNamesJoined (fields: NamedField list) =
    [for f in fields -> f.Name]
    |> String.concat ", "

type PopValueContext =
    | NewVariable of name: string
    | ExistingVar of name: string
    | Return
    | SafeReturn

let popValue (writer: TextWriter) (level: int) (type_: NIType) (context: PopValueContext) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let contextPrefix =
        match context with
        | NewVariable name -> sprintf "var %s =" name
        | ExistingVar name -> sprintf "%s =" name
        | Return -> "return"
        | SafeReturn -> "var __ret ="
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
        | EnumType name -> write level "%s __%sArray__Pop();" contextPrefix (upperFirst name)
        | StructType name -> write level "%s __%sArray__Pop();" contextPrefix (upperFirst name)
        | ArrayType subType -> write level "%s __%sArray__Pop();" contextPrefix (typeToName (ArrayType subType))
        | MapType _ -> write level "%s __%s__Pop();" contextPrefix (typeToName type_)
        | _ -> write level "// popValue not yet supported: array of %A" niType
    | MapType _ ->
        write level "%s __%s__Pop();" contextPrefix (typeToName type_)
    | StructType name ->
        write level "%s %s__Pop();" contextPrefix (upperFirst name)
    | EnumType name ->
        write level "%s %s__Pop();" contextPrefix (upperFirst name)
    | SumType name ->
        write level "%s %s__Pop();" contextPrefix (upperFirst name)
    | TypedefType name ->
        write level "%s %s__Pop();" contextPrefix (upperFirst name)
    | _ ->
        write level "// popValue %A not yet supported" type_
    match context with
    | SafeReturn ->
        write level "NativeImplClient.ServerClearSafetyArea();"
        write level "return __ret;"
    | _ ->
        ()

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
        | EnumType name -> write level "__%sArray__Push(%s);" (upperFirst name) content
        | StructType name -> write level "__%sArray__Push(%s, %s);" (upperFirst name) content (isReturnString())
        | TypedefType name -> write level "__%sArray__Push(%s, %s);" (upperFirst name) content (isReturnString())
        | ArrayType subType -> write level "__%sArray__Push(%s, %s);" (typeToName (ArrayType subType)) content (isReturnString())
        | MapType _ -> write level "__%s__Push(%s, %s);" (typeToName type_) content (isReturnString())
        | _ -> write level "// pushValue not yet supported: Array of %A" niType
    | EnumType name ->
        write level "%s__Push(%s);" name content
    | SumType name ->
        write level "%s__Push(%s, %s);" name content (isReturnString())
    | StructType name ->
        write level "%s__Push(%s, %s);" name content (isReturnString())
    | TypedefType name ->
        write level "%s__Push(%s, %s);" name content (isReturnString())
    | MapType _ ->
        write level "__%s__Push(%s, %s);" (typeToName type_) content (isReturnString())
    | _ ->
        write level "// pushValue %A not yet supported" type_

let renderLibraryUserTypes (writer: TextWriter) (containsRefs: NIType -> bool) (name: string) (content: UserOrGeneric) (typeMap: Map<string, UserOrGeneric>) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""
    //
    blank()
    match content with
    | UserOrGeneric.FwdInterface ->
        () // C# doesn't require forward declarations :)
    | UserOrGeneric.Generic gType ->
        match gType with
        | TupleType tupleTypes ->
            let typesJoined =
                tupleTypes |> List.map (typeToCsString typeMap) |> String.concat ", "
            // PUSH =============
            write 2 "private static void __%s__Push((%s) value, bool isReturn)" (typeToName gType) typesJoined
            write 2 "{"
            for i, type_ in tupleTypes |> List.indexed |> List.rev do
                pushValue writer 3 type_ (sprintf "value.Item%d" (i+1)) (Indirect "isReturn")
            write 2 "}"
            // POP ==============
            blank()
            write 2 "private static (%s) __%s__Pop()" typesJoined (typeToName gType)
            write 2 "{"
            for i, type_ in tupleTypes |> List.indexed do
                popValue writer 3 type_ (NewVariable (sprintf "_%d" i))
            let varsJoined =
                tupleTypes |> List.indexed |> List.map (fun (i, _) -> sprintf "_%d" i) |> String.concat ", "
            write 3 "return (%s);" varsJoined
            write 2 "}"
        | ArrayType arrayType ->
            let arrayCommon (name: string) (elementType: NIType) =
                // use this for any types with no particular specializations (typedefs, delegates, arrays of arrays -- everything but structs and tuples, basically)
                // PUSH ========
                write 2 "private static void __%sArray__Push(%s[] values, bool isReturn)" (typeToName elementType) (typeToCsString typeMap elementType)
                write 2 "{"
                write 3 "foreach (var value in values.Reverse())"
                write 3 "{"
                pushValue writer 4 elementType "value" (Indirect "isReturn")
                write 3 "}"
                write 3 "NativeImplClient.PushSizeT((IntPtr)values.Length);"
                write 2 "}"
                // POP =========
                blank()
                write 2 "private static %s[] __%sArray__Pop()" (typeToCsString typeMap elementType) (typeToName elementType)
                write 2 "{"
                write 3 "var count = (int)NativeImplClient.PopSizeT();"
                write 3 "var ret = %s;" (newCsArray typeMap elementType "count")
                write 3 "for (var i = 0; i < count; i++)"
                write 3 "{"
                popValue writer 4 elementType (ExistingVar "ret[i]")
                write 3 "}"
                write 3 "return ret;"
                write 2 "}"
            match arrayType with
            | TypedefType typeDefName ->
                match typeMap[typeDefName] with
                | UserOrGeneric.User (TypeDef (TupleType tupleTypes)) ->
                    // array of typedef of tuple -> flatten to array of tuple
                    let joinedTypes =
                        tupleTypes |> List.map (typeToCsString typeMap) |> String.concat ", "
                    // PUSH ====================
                    write 2 "private static void __%sArray__Push((%s)[] things, bool isReturn)" typeDefName joinedTypes
                    write 2 "{"
                    write 3 "var count = things.Length;"
                    for i, type_ in tupleTypes |> List.indexed do
                        write 3 "var _%dvalues = new %s[count];" i (typeToCsString typeMap type_)
                    write 3 "for (var i = 0; i < count; i++)"
                    write 3 "{"
                    for i, _ in tupleTypes |> List.indexed do
                        write 4 "_%dvalues[i] = things[i].Item%d;" i (i + 1)
                    write 3 "}"
                    for i, type_ in tupleTypes |> List.indexed |> List.rev do
                        pushValue writer 3 (ArrayType type_) (sprintf "_%dvalues" i) (Indirect "isReturn")
                    write 2 "}"
                    // POP =====================
                    blank()
                    write 2 "private static (%s)[] __%sArray__Pop()" joinedTypes typeDefName
                    write 2 "{"
                    for i, type_ in tupleTypes |> List.indexed do
                        popValue writer 3 (ArrayType type_) (NewVariable (sprintf "_%dvalues" i))
                    write 3 "var count = _0values.Length;"
                    write 3 "var ret = new (%s)[count];" joinedTypes
                    write 3 "for (var i = 0; i < count; i++)"
                    write 3 "{"
                    for i, _ in tupleTypes |> List.indexed do
                        write 4 "var _%d = _%dvalues[i];" i i
                    let joinedVars =
                        tupleTypes |> List.indexed |> List.map (fun (i, _) -> sprintf "_%d" i) |> String.concat ", "
                    write 4 "ret[i] = (%s);" joinedVars
                    write 3 "}"
                    write 3 "return ret;"
                    write 2 "}"
                | _ ->
                    failwithf "array of typedef %A: TODO (basic array treatment)" typeDefName
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
                    write 2 "private static void __%sArray__Push(%s[] items, bool isReturn)" (upperFirst structName) (upperFirst structName)
                    write 2 "{"
                    write 3 "var count = items.Length;"
                    for i, field in namedFields |> List.indexed do
                        write 3 "var f%dValues = %s;" i (newCsArray typeMap field.Type_ "count")
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
                    write 2 "private static %s[] __%sArray__Pop()" (upperFirst structName) (upperFirst structName)
                    write 2 "{"
                    for i, field in namedFields |> List.indexed do
                        popValue writer 3 (ArrayType field.Type_) (NewVariable (sprintf "f%dValues" i))
                    write 3 "var count = f0Values.Length;"
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
            | ArrayType _ ->
                // array of array of ...
                arrayCommon (typeToName arrayType) arrayType
            | MapType _ ->
                arrayCommon (typeToName arrayType) arrayType
            // ignore built-ins below:
            | Bool
            | Int8 | Int16 | Int32 | Int64
            | Float32 | Float64
            | StringType ->
                // we don't handle any of the primitives, those are all built-in
                write 2 "// built-in array type: %s" (typeToCsString typeMap gType)
            | _ ->
                write 2 "// TODO: push/pop for generic %s" (typeToCsString typeMap gType)
        | MapType (keyType, valueType) ->
            // push
            write 2 "static void __%s__Push(Dictionary<%s, %s> map, bool isReturn)" (typeToName gType) (typeToCsString typeMap keyType) (typeToCsString typeMap valueType)
            write 2 "{"
            pushValue writer 3 (ArrayType valueType) (sprintf "map.Values.ToArray()") (Indirect "isReturn")
            pushValue writer 3 (ArrayType keyType) (sprintf "map.Keys.ToArray()") (Indirect "isReturn")
            write 2 "}"
            // pop
            blank()
            write 2 "static Dictionary<%s, %s> __%s__Pop()" (typeToCsString typeMap keyType) (typeToCsString typeMap valueType) (typeToName gType)
            write 2 "{"
            popValue writer 3 (ArrayType keyType) (NewVariable "keys")
            popValue writer 3 (ArrayType valueType) (NewVariable "values")
            write 3 "var ret = new Dictionary<%s,%s>();" (typeToCsString typeMap keyType) (typeToCsString typeMap valueType)
            write 3 "for (var i = 0; i < keys.Length; i++)"
            write 3 "{"
            write 4 "ret[keys[i]] = values[i];"
            write 3 "}"
            write 3 "return ret;"
            write 2 "}"
        | _ ->
            write 2 "// TODO: renderLibraryUserType: generic %s" (typeToCsString typeMap gType)
    | UserOrGeneric.User userType ->
        match userType with
        | OpaqueHandleDef ->
            write 2 "// anything for opaque here? push/pop perhaps? %s" name
        | StructDef namedFields ->
            write 2 "public struct %s {" name
            for field in namedFields do
                write 3 "public %s %s;" (typeToCsString typeMap field.Type_) (upperFirst field.Name)
            // constructor
            write 3 "public %s(%s)" name (fieldsJoined typeMap namedFields)
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
        | SumTypeDef sumTypeValues ->
            // definition
            write 2 "public abstract record %s" (upperFirst name)
            write 2 "{"
            for value in sumTypeValues do
                if value.Args.IsEmpty then
                    write 3 "public sealed record %s : %s;" (upperFirst value.Name) name
                else
                    let upperFirstArgs =
                        value.Args |> List.map (fun (name, t) -> (upperFirst name), t)
                    write 3 "public sealed record %s(%s) : %s" (upperFirst value.Name) (argsJoined typeMap upperFirstArgs) name
                    write 3 "{"
                    for argName, argType in upperFirstArgs do
                        write 4 "public %s %s { get; } = %s;" (typeToCsString typeMap argType) argName argName
                    write 3 "}"
            write 2 "}"
            // tags for push/pop
            blank()
            write 2 "private enum %s__Tag" (upperFirst name)
            write 2 "{"
            for value, maybeComma in sumTypeValues |> withTrailingCommas do
                write 3 "%s%s" value.Name maybeComma
            write 2 "}"
            // PUSH
            blank()
            write 2 "private static void %s__Push(%s thing, bool isReturn)" (upperFirst name) (upperFirst name)
            write 2 "{"
            write 3 "%s__Tag which;" (upperFirst name)
            write 3 "switch (thing)"
            write 3 "{"
            for value in sumTypeValues do
                write 4 "case %s.%s %s:" (upperFirst name) (upperFirst value.Name) (lowerFirst value.Name)
                write 5 "which = %s__Tag.%s;" (upperFirst name) (upperFirst value.Name)
                for argName, argType in value.Args |> List.rev do
                    pushValue writer 5 argType (sprintf "%s.%s" (lowerFirst value.Name) (upperFirst argName)) (Indirect "isReturn")
                write 5 "break;"
            write 4 "default:"
            write 5 "throw new ArgumentOutOfRangeException(nameof(thing));"
            write 3 "}"
            write 3 "NativeImplClient.PushInt((int)which);"
            write 2 "}"
            // POP
            blank()
            write 2 "private static %s %s__Pop()" (upperFirst name) (upperFirst name)
            write 2 "{"
            write 3 "var which = (%s__Tag)NativeImplClient.PopInt();" (upperFirst name)
            write 3 "switch (which)"
            write 3 "{"
            for value in sumTypeValues do
                write 4 "case %s__Tag.%s:" (upperFirst name) (upperFirst value.Name)
                write 4 "{"
                for argName, argType in value.Args do
                    popValue writer 5 argType (NewVariable argName)
                write 5 "return new %s.%s(%s);" (upperFirst name) (upperFirst value.Name) (argNamesJoined value.Args)
                write 4 "}"
            write 4 "default:"
            write 5 "throw new ArgumentOutOfRangeException();"
            write 3 "}"
            write 2 "}"
        | TypeDef niType ->
            // C# has no proper typedefs, so we just emit push/pop that pass through to the real thing
            // (at least it will make wrapper funcs a bit clearer)
            // PUSH
            write 2 "[MethodImpl(MethodImplOptions.AggressiveInlining)]"
            write 2 "private static void %s__Push(%s value, bool isReturn)" name (typeToCsString typeMap niType)
            write 2 "{"
            write 3 "__%s__Push(value, isReturn);" (typeToName niType)
            write 2 "}"
            // POP
            blank()
            write 2 "[MethodImpl(MethodImplOptions.AggressiveInlining)]"
            write 2 "private static %s %s__Pop()" (typeToCsString typeMap niType) name
            write 2 "{"
            write 3 "return __%s__Pop();" (typeToName niType)
            write 2 "}"
        | InterfaceDef namedFunctions ->
            blank()
            // raw interface itself
            write 2 "public interface %s : IDisposable" (upperFirst name)
            write 2 "{"
            for method in namedFunctions do
                let throwsComment =
                    match method.Throws with
                    | Some eType -> sprintf " // throws %s" (typeToCsString typeMap eType)
                    | None -> ""
                write 3 "%s %s(%s);%s" (typeToCsString typeMap method.Returns) (upperFirst method.Name) (argsJoined typeMap method.Args) throwsComment
            write 2 "}"
            // pop template
            blank()
            writer.WriteLine(Regex(@"%%IFACE%%").Replace(interfacePopTemplate, name))
            // client base class
            blank()
            write 2 "public abstract class Client%s : NativeImplClient.ClientObject, %s" (upperFirst name) (upperFirst name)
            write 2 "{"
            write 3 "public virtual void Dispose()"
            write 3 "{"
            write 4 "// override if necessary"
            write 3 "}"
            // abstract methods for client base class
            for method in namedFunctions do
                let throwsComment =
                    match method.Throws with
                    | Some eType -> sprintf " // throws %s" (typeToCsString typeMap eType)
                    | None -> ""
                write 3 "public abstract %s %s(%s);%s" (typeToCsString typeMap method.Returns) (upperFirst method.Name) (argsJoined typeMap method.Args) throwsComment
            write 2 "}"
            // server wrapper
            blank()
            write 2 "private class Server%s : NativeImplClient.ServerObject, %s" (upperFirst name) (upperFirst name)
            write 2 "{"
            write 3 "public Server%s(int id) : base(id)" name
            write 3 "{"
            write 3 "}"
            // server wrapper methods
            for method in namedFunctions do
                blank()
                write 3 "public %s %s(%s)" (typeToCsString typeMap method.Returns) (upperFirst method.Name) (argsJoined typeMap method.Args)
                write 3 "{"
                // push args
                for argName, argType in method.Args do
                    pushValue writer 4 argType argName (Direct false)
                // invoke remote
                match method.Throws with
                | Some _ ->
                    write 4 "NativeImplClient.InvokeInterfaceMethodWithExceptions(_%s_%s, Id);" (lowerFirst name) method.Name
                | None ->
                    write 4 "NativeImplClient.InvokeInterfaceMethod(_%s_%s, Id);" (lowerFirst name) method.Name
                // return (mindful of safety area nonsense)
                if containsRefs method.Returns then
                    popValue writer 4 method.Returns (NewVariable "__ret")
                    write 4 "NativeImplClient.ServerClearSafetyArea();"
                    write 4 "return __ret;"
                else
                    popValue writer 4 method.Returns Return
                write 3 "}"
            blank()
            write 3 "public void Dispose()"
            write 3 "{"
            write 4 "ServerDispose();"
            write 3 "}"
            write 2 "}"
        | ExceptionDef fields ->
            write 2 "public class %s : Exception" name
            write 2 "{"
            for f in fields do
                write 3 "public readonly %s %s;" (typeToCsString typeMap f.Type_) (upperFirst f.Name)
            write 3 "public %s(%s) : base(\"%s\")" name (fieldsJoined typeMap fields) name
            write 3 "{"
            for f in fields do
                write 4 "%s = %s;" (upperFirst f.Name) f.Name
            write 3 "}"
            write 3 "internal void PushAndSet()"
            write 3 "{"
            for f in fields |> List.rev do
                pushValue writer 4 f.Type_ (upperFirst f.Name) (Direct true) // true since pushed exceptions are basically returns, any reference fields need to be safely pushed
            write 4 "NativeImplClient.SetException(_%s);" (lowerFirst name)
            write 3 "}"
            write 3 "internal static void BuildAndThrow()"
            write 3 "{"
            for f in fields do
                popValue writer 4 f.Type_ (NewVariable f.Name)
            let namesJoined =
                [for f in fields -> f.Name] |> String.concat ", "
            write 4 "throw new %s(%s);" name namesJoined
            write 3 "}"
            write 2 "}"
        | DelegateDef info ->
            write 2 "public delegate %s %s(%s);" (typeToCsString typeMap info.Returns) name (argsJoined typeMap info.Args)
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
            match info.Throws with
            | Some (ExceptionType exceptionName as exc) ->
                write 4 "try"
                write 4 "{"
                pushValue writer 5 info.Returns invocation (Direct true)
                write 4 "}"
                write 4 "catch (%s e)" (upperFirst exceptionName)
                write 4 "{"
                write 5 "e.PushAndSet();"
                write 4 "}"
            | _ ->
                pushValue writer 5 info.Returns invocation (Direct true)
            write 3 "}"
            write 3 "NativeImplClient.PushClientFuncVal(CallbackWrapper, Marshal.GetFunctionPointerForDelegate(callback));"
            write 2 "}"
            // pop
            blank()
            write 2 "private static %s %s__Pop()" name name
            write 2 "{"
            write 3 "var id = NativeImplClient.PopServerFuncValId();"
            write 3 "var remoteFunc = new NativeImplClient.ServerFunc(id);"
            write 3 "%s Wrapper(%s)" (typeToCsString typeMap info.Returns) (argsJoined typeMap info.Args)
            write 3 "{"
            for argName, argType in info.Args |> List.rev do
                pushValue writer 4 argType argName (Direct false)
            match info.Throws with
            | Some _ ->
                write 4 "remoteFunc.ExecWithExceptions();"
            | None ->
                write 4 "remoteFunc.Exec();"
            popValue writer 4 info.Returns Return
            write 3 "}"
            write 3 "return Wrapper;"
            write 2 "}"

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
    write 0 "using System.Runtime.InteropServices;"
    write 0 ""
    write 0 "namespace %sClient" m.Name
    write 0 "{"
    write 1 "public static class %s" m.Name
    write 1 "{"
    write 2 "private static ModuleHandle _module;"

    let typeMap =
        userTypesAndGenerics m

    // top level functions
    for namedFunc in moduleMethods do
        write 2 "private static ModuleMethodHandle _%s;" namedFunc.Name

    // interfaces
    for name, methods in interfaces do
        write 2 "private static InterfaceHandle _%s;" (lowerFirst name)
        for method in methods do
            write 2 "private static InterfaceMethodHandle _%s_%s;" (lowerFirst name) method.Name

    // exceptions
    for name, _ in exceptions do
        write 2 "private static ExceptionHandle _%s;" (lowerFirst name)

    // globals declaration
    for name, type_ in globals do
        write 2 "public static %s %s { get; private set; }" (typeToCsString typeMap type_) (upperFirst name)

    // user types ====================
    let containsRefsMap =
        createContainsRefsMap typeMap
    let containsRefs (type_: NIType) =
        containsRefsMap.ContainsKey(typeToInternalKey type_)
    let renderFunc =
        renderLibraryUserTypes writer containsRefs
    renderUserTypes typeMap renderFunc

    // top-level functions ===========
    for namedFunc in moduleMethods do
        blank()
        write 2 "public static %s %s(%s)" (typeToCsString typeMap namedFunc.Returns) (upperFirst namedFunc.Name) (argsJoined typeMap namedFunc.Args)
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
        popValue writer 3 namedFunc.Returns (if containsRefs namedFunc.Returns then SafeReturn else Return)
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
            write 3 "NativeImplClient.SetExceptionBuilder(_%s, %s.BuildAndThrow);" (lowerFirst name) name

    // interface method handles, wrappers
    if not interfaces.IsEmpty then
        for ifaceName, methods in interfaces do
            blank()
            write 3 "_%s = NativeImplClient.GetInterface(_module, \"%s\");" (lowerFirst ifaceName) ifaceName
            // methods
            if not methods.IsEmpty then
                for method in methods do
                    write 3 "_%s_%s = NativeImplClient.GetInterfaceMethod(_%s, \"%s\");" (lowerFirst ifaceName) (lowerFirst method.Name) (lowerFirst ifaceName) method.Name
            // method wrappers
            for method in methods do
                blank()
                write 3 "NativeImplClient.SetClientMethodWrapper(_%s_%s, delegate(NativeImplClient.ClientObject obj)" (lowerFirst ifaceName) method.Name
                write 3 "{"
                write 4 "var inst = (Client%s) obj;" ifaceName
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
                    write 4 "catch (%s e)" (typeToCsString typeMap eType)
                    write 4 "{"
                    write 5 "NativeImplClient.SetException(_%s);" (lowerFirst (typeToCsString typeMap eType))
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
    write 1 "}" // end of static class <module name>
    //
    write 0 "}" // end of namespace

let generate (m: Module) =
    printfn "C# writing TestLibrary.cs"
    use testLibraryCs = new StreamWriter(@"F:\Workbench\nativeimpl\client\csharp\testproject\TestLibraryClient\TestLibrary.cs")
    writeLibrary m testLibraryCs
