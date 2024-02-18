module Generators.Frontend.CsharpGenerator

open System.IO
open Parser

open Util
open Generators.Common

open AppCommon

let resolvedNameToString (name: ResolvedName) =
    match name with
    | ThisModule (SymbolName nameParts) ->
        joinNameParts (nameParts |> List.map upperFirst)
    | Imported(ModuleName moduleNameParts, SymbolName nameParts) ->
        let x =
            (moduleNameParts @ nameParts) |> List.map upperFirst
        joinNameParts x
        
let typeToCsString (context: ModuleContext) (outerType: NIType) =
    let rec recInner = function
        | UnitType -> "void"
        | Primitive prim ->
            match prim with
            | SizeT -> "IntPtr"
            | Bool -> "bool"
            | Int(signed, intWidth) ->
                match signed, intWidth with
                | true, Width8 -> "sbyte"
                | false, Width8 -> "byte"
                | true, Width16 -> "short"
                | false, Width16 -> "ushort"
                | true, Width32 -> "int"
                | false, Width32 -> "uint"
                | true, Width64 -> "long"
                | false, Width64 -> "ulong"
            | Float32 -> "float"
            | Float64 -> "double"
            | StringType -> "string"
        | Resolved (resolvedName, resolvedType) ->
            match resolvedType with
            | TypedefType ->
                // no C# typedefs so we just render the type directly
                let content =
                    requiredSymbolLookup context resolvedName
                match content with
                | TypeDef actual ->
                    recInner actual
                | _ ->
                    failwithf "typeToCsString: failed to find typedef definition"
            | _ ->
                resolvedNameToString resolvedName
        | ArrayType elemType -> sprintf "%s[]" (recInner elemType)
        | SetType elemType -> sprintf "HashSet<%s>" (recInner elemType)
        | BufferType elemType -> sprintf "INativeBuffer<%s>" (recInner elemType)
        | TupleType tupleTypes ->
            let joined =
                tupleTypes
                |> List.map recInner
                |> String.concat ", "
            sprintf "(%s)" joined
        | MapType (keyType, valueType) ->
            sprintf "Dictionary<%s,%s>" (recInner keyType) (recInner valueType)
        | x ->
            sprintf "(typeToCsString: not yet supported: %A)" x
    // go
    recInner outerType

let rec newCsArray (context: ModuleContext) (ofType: NIType) (countVar: string) =
    match ofType with
    | ArrayType niType ->
        sprintf "%s[]" (newCsArray context niType countVar)
    | _ ->
        sprintf "new %s[%s]" (typeToCsString context ofType) countVar

let rec typeToName = function
    | UnitType -> failwith "typeToName: 'unit' type as name?"
    | Primitive prim ->
        match prim with
        | SizeT -> "SizeT"
        | Bool -> "Bool"
        | Int(signed, width) ->
            match signed, width with
            | true, Width8 -> "SByte"
            | false, Width8 -> "Byte"
            | true, Width16 -> "Short"
            | false, Width16 -> "UShort"
            | true, Width32 -> "Int"
            | false, Width32 -> "UInt"
            | true, Width64 -> "Long"
            | false, Width64 -> "ULong"
        | Float32 -> "Float"
        | Float64 -> "Double"
        | StringType -> "String"
    | Resolved (resolvedName, _) ->
        resolvedNameToString resolvedName
    | TupleType tupleTypes ->
        let joined =
            tupleTypes
            |> List.map typeToName
            |> String.concat ""
        sprintf "%s_Tuple" joined
    | ArrayType niType ->
        sprintf "%s_Array" (typeToName niType)
    | SetType niType ->
        sprintf "%s_Set" (typeToName niType)
    | MapType (keyType, valueType) ->
        sprintf "%s_%s_Map" (typeToName keyType) (typeToName valueType)
    | BufferType elemType ->
        sprintf "Native_%s_Buffer" (typeToName elemType)
    | x -> sprintf "(typeToName: not yet supported: %A)" x

let argsJoined (context: ModuleContext) (args: (string * NIType) list) =
    [for name, type_ in args -> sprintf "%s %s" (typeToCsString context type_) name]
    |> String.concat ", "

let argNamesJoined (args: (string * NIType) list) =
    [for name, _ in args -> name]
    |> String.concat ", "

let fieldsJoined (context: ModuleContext) (fields: NamedField list) =
    [for f in fields -> sprintf "%s %s" (typeToCsString context f.Type_) f.Name]
    |> String.concat ", "

let fieldNamesJoined (fields: NamedField list) =
    [for f in fields -> f.Name]
    |> String.concat ", "

type PopValueContext =
    | NewVariable of name: string
    | ExistingVar of name: string
    | Return
    | SafeReturn

let popValue (writer: IFormatWriter) (level: int) (type_: NIType) (context: PopValueContext) =
    let write = writer.Write
    let contextPrefix =
        match context with
        | NewVariable name -> sprintf "var %s =" name
        | ExistingVar name -> sprintf "%s =" name
        | Return -> "return"
        | SafeReturn -> "var __ret ="
    match type_ with
    | UnitType ->
        match context with
        | Return -> () // nothing'
        | _ -> failwith "tried to pop a Unit value in a non-Return context, why?"
    | Primitive prim ->
        match prim with
        | SizeT -> write level "%s NativeImplClient.PopSizeT();" contextPrefix
        | Bool -> write level "%s NativeImplClient.PopBool();" contextPrefix
        | Int(signed, width) ->
            write level "%s NativeImplClient.Pop%sInt%d();" contextPrefix (if signed then "" else "U") (widthToInt width)
        | Float32 -> write level "%s NativeImplClient.PopFloat();" contextPrefix
        | Float64 -> write level "%s NativeImplClient.PopDouble();" contextPrefix
        | StringType -> write level "%s NativeImplClient.PopString();" contextPrefix
    | Resolved (resolvedName, _) ->
        let name =
            resolvedNameToString resolvedName
        write level "%s %s__Pop();" contextPrefix name
    | ArrayType niType ->
        match niType with
        | Primitive prim ->
            match prim with
            | Bool -> write level "%s NativeImplClient.PopBoolArray();" contextPrefix
            | Int(signed, width) ->
                write level "%s NativeImplClient.Pop%sInt%dArray();" contextPrefix (if signed then "" else "U") (widthToInt width)
            | Float32 -> write level "%s NativeImplClient.PopFloatArray();" contextPrefix
            | Float64 -> write level "%s NativeImplClient.PopDoubleArray();" contextPrefix
            | StringType -> write level "%s NativeImplClient.PopStringArray();" contextPrefix
            | _ -> write level "// popValue not yet supported: array of prim %A" prim
        | Resolved _ ->
            write level "%s __%s__Pop();" contextPrefix (typeToName (ArrayType niType))
        | ArrayType _ -> write level "%s __%s__Pop();" contextPrefix (typeToName type_) // sigh, normalize all these already - doing it so many different ways...
        | SetType _ -> write level "%s __%s__Pop();" contextPrefix (typeToName type_)
        | MapType _ -> write level "%s __%s__Pop();" contextPrefix (typeToName type_)
        | TupleType _ -> write level "%s __%s__Pop();" contextPrefix (typeToName type_)
        | _ -> write level "// popValue not yet supported: array of %A" niType
    | MapType _ ->
        write level "%s __%s__Pop();" contextPrefix (typeToName type_)
    | TupleType _ ->
        write level "%s __%s__Pop();" contextPrefix (typeToName type_)
    | SetType _ ->
        write level "%s __%s__Pop();" contextPrefix (typeToName type_)
    | BufferType _ ->
        write level "%s __%s__Pop();" contextPrefix (typeToName type_)
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

let pushValue (writer: IFormatWriter) (level: int) (type_: NIType) (content: string) (isReturn: PushIsReturn) =
    let write = writer.Write
    let isReturnString() =
        match isReturn with
        | Direct value -> if value then "true" else "false"
        | Indirect name -> name
        | NotApplicable -> failwith "pushValue: 'NotApplicable' isReturn was consumed"
    match type_ with
    | UnitType ->
        write level "%s;" content // content directly (method invocation etc)
    | Primitive prim ->
        match prim with
        | SizeT -> write level "NativeImplClient.PushSizeT(%s);" content
        | Bool -> write level "NativeImplClient.PushBool(%s);" content
        | Int(signed, width) ->
            write level "NativeImplClient.Push%sInt%d(%s);" (if signed then "" else "U") (widthToInt width) content
        | Float32 -> write level "NativeImplClient.PushFloat(%s);" content
        | Float64 -> write level "NativeImplClient.PushDouble(%s);" content
        | StringType -> write level "NativeImplClient.PushString(%s);" content
    | Resolved(resolvedName, resolvedType) ->
        let name =
            resolvedNameToString resolvedName
        match resolvedType with
        | StructType | InterfaceType | TypedefType | SumType | OptionsType ->
            // types that potentially contain refs, need to honor isReturn
            write level "%s__Push(%s, %s);" name content (isReturnString())
        | _ ->
            write level "%s__Push(%s);" name content
    | ArrayType niType ->
        match niType with
        | Primitive prim ->
            match prim with
            | Bool -> write level "NativeImplClient.PushBoolArray(%s);" content
            | Int(signed, width) ->
                write level "NativeImplClient.Push%sInt%dArray(%s);" (if signed then "" else "U") (widthToInt width) content
            | Float32 -> write level "NativeImplClient.PushFloatArray(%s);" content
            | Float64 -> write level "NativeImplClient.PushDoubleArray(%s);" content
            | StringType -> write level "NativeImplClient.PushStringArray(%s);" content
            | _ -> write level "// pushValue not yet supported: Array of prim %A" prim
        | Resolved (_, resolvedType) ->
            match resolvedType with
            | StructType | InterfaceType | TypedefType | SumType | OptionsType ->
                write level "__%s__Push(%s, %s);" (typeToName (ArrayType niType)) content (isReturnString())
            | _ ->
                write level "__%s__Push(%s);" (typeToName (ArrayType niType)) content
        | ArrayType subType -> write level "__%s__Push(%s, %s);" (typeToName type_) content (isReturnString())
        | SetType subType -> write level "__%s__Push(%s, %s);" (typeToName type_) content (isReturnString())
        | MapType _ -> write level "__%s__Push(%s, %s);" (typeToName type_) content (isReturnString())
        | TupleType _ -> write level "__%s__Push(%s, %s);" (typeToName type_) content (isReturnString())
        | _ -> write level "// pushValue not yet supported: Array of %A" niType
    | TupleType _ ->
        write level "__%s__Push(%s, %s);" (typeToName type_) content (isReturnString())
    | SetType _ ->
        write level "__%s__Push(%s, %s);" (typeToName type_) content (isReturnString())
    | BufferType _ ->
        write level "__%s__Push(%s, %s);" (typeToName type_) content (isReturnString())
    | MapType _ ->
        write level "__%s__Push(%s, %s);" (typeToName type_) content (isReturnString())
    | _ ->
        write level "// pushValue %A not yet supported" type_

// type ArrayRenderer(writer: IFormatWriter, typeMap: Map<string, UserOrGeneric>, outerType: NIType, elemType: NIType) =
//     let write, blank = writer.Write, writer.Blank
//     let arrayDefault () =
//         // use this for any types with no particular specializations (typedefs, delegates, arrays of arrays -- everything but structs and tuples, basically)
//         // PUSH ========
//         write 2 "internal static void __%s__Push(%s values, bool isReturn)" (typeToName outerType) (typeToCsString typeMap outerType)
//         write 2 "{"
//         write 3 "foreach (var value in values.Reverse())"
//         write 3 "{"
//         pushValue writer 4 elemType "value" (Indirect "isReturn")
//         write 3 "}"
//         write 3 "NativeImplClient.PushSizeT((IntPtr)values.Length);"
//         write 2 "}"
//         // POP =========
//         blank()
//         write 2 "internal static %s __%s__Pop()" (typeToCsString typeMap outerType) (typeToName outerType)
//         write 2 "{"
//         write 3 "var count = (int)NativeImplClient.PopSizeT();"
//         write 3 "var ret = %s;" (newCsArray typeMap elemType "count")
//         write 3 "for (var i = 0; i < count; i++)"
//         write 3 "{"
//         popValue writer 4 elemType (ExistingVar "ret[i]")
//         write 3 "}"
//         write 3 "return ret;"
//         write 2 "}"
//     interface IArrayLikeRenderer with
//         member this.TypedefTuple (tt: NIType) =
//             // new, flattened outer (minus the typedef middleman)
//             // then the thin (inlined) wrappers around it
//             // push ==================
//             write 2 "[MethodImpl(MethodImplOptions.AggressiveInlining)]"
//             write 2 "internal static void __%s__Push(%s values, bool isReturn)" (typeToName outerType) (typeToCsString typeMap outerType)
//             write 2 "{"
//             pushValue writer 3 (ArrayType tt) "values" (Indirect "isReturn")
//             write 2 "}"
//             // pop ===================
//             blank()
//             write 2 "[MethodImpl(MethodImplOptions.AggressiveInlining)]"
//             write 2 "internal static %s __%s__Pop()" (typeToCsString typeMap outerType) (typeToName outerType)
//             write 2 "{"
//             popValue writer 3 (ArrayType tt) Return
//             write 2 "}"
//         member this.EnumType (name: string) =
//             let widthType =
//                 match typeMap[name] with
//                 | UserOrGeneric.User (EnumDef info) ->
//                     let signed =
//                         not info.FlagsMode
//                     info.MaybeWidth
//                     |> Option.defaultValue (Primitive(Int(signed,Width32)))
//                 | _ -> failwith "ArrayRenderer this.EnumType trouble!!!"
//             // push
//             write 2 "internal static void __%s__Push(%s items)" (typeToName outerType) (typeToCsString typeMap outerType)
//             write 2 "{"
//             write 3 "var intValues = items.Select(i => (%s)i).ToArray();" (typeToCsString typeMap widthType)
//             pushValue writer 3 (ArrayType widthType) "intValues" NotApplicable
//             write 2 "}"
//             // pop
//             blank()
//             write 2 "internal static %s __%s__Pop()" (typeToCsString typeMap outerType) (typeToName outerType)
//             write 2 "{"
//             popValue writer 3 (ArrayType widthType) (NewVariable "intValues")
//             write 3 "return intValues.Select(i => (%s)i).ToArray();" (typeToCsString typeMap elemType)
//             write 2 "}"
//         member this.StructType (structName: string) (namedFields: NamedField list) =
//             // push
//             write 2 "internal static void __%s__Push(%s items, bool isReturn)" (typeToName outerType) (typeToCsString typeMap outerType)
//             write 2 "{"
//             write 3 "var count = items.Length;"
//             for i, field in namedFields |> List.indexed do
//                 write 3 "var f%dValues = %s;" i (newCsArray typeMap field.Type_ "count")
//             write 3 "for (var i = 0; i < count; i++)"
//             write 3 "{"
//             for i, field in namedFields |> List.indexed do
//                 write 4 "f%dValues[i] = items[i].%s;" i (upperFirst field.Name)
//             write 3 "}"
//             // push in reverse order
//             for i, field in namedFields |> List.indexed |> List.rev do
//                 pushValue writer 3 (ArrayType field.Type_) (sprintf "f%dValues" i) (Indirect "isReturn")
//             write 2 "}"
//             // pop
//             blank()
//             write 2 "internal static %s __%s__Pop()" (typeToCsString typeMap outerType) (typeToName outerType)
//             write 2 "{"
//             for i, field in namedFields |> List.indexed do
//                 popValue writer 3 (ArrayType field.Type_) (NewVariable (sprintf "f%dValues" i))
//             write 3 "var count = f0Values.Length;"
//             write 3 "var ret = new %s[count];" (typeToCsString typeMap elemType)
//             write 3 "for (var i = 0; i < count; i++)"
//             write 3 "{"
//             for i, _ in namedFields |> List.indexed do
//                 write 4 "var f%d = f%dValues[i];" i i
//             let fieldVarsJoined =
//                 namedFields |> List.indexed |> List.map (fun (i, _) -> sprintf "f%d" i) |> String.concat ", "
//             write 4 "ret[i] = new %s(%s);" (typeToCsString typeMap elemType) fieldVarsJoined
//             write 3 "}"
//             write 3 "return ret;"
//             write 2 "}"
//         member this.TupleType (tupleTypes: NIType list) =
//             // PUSH ====================
//             write 2 "internal static void __%s__Push(%s things, bool isReturn)" (typeToName outerType) (typeToCsString typeMap outerType)
//             write 2 "{"
//             write 3 "var count = things.Length;"
//             for i, type_ in tupleTypes |> List.indexed do
//                 write 3 "var _%dvalues = new %s[count];" i (typeToCsString typeMap type_)
//             write 3 "for (var i = 0; i < count; i++)"
//             write 3 "{"
//             for i, _ in tupleTypes |> List.indexed do
//                 write 4 "_%dvalues[i] = things[i].Item%d;" i (i + 1)
//             write 3 "}"
//             for i, type_ in tupleTypes |> List.indexed |> List.rev do
//                 pushValue writer 3 (ArrayType type_) (sprintf "_%dvalues" i) (Indirect "isReturn")
//             write 2 "}"
//             // POP =====================
//             blank()
//             write 2 "internal static %s __%s__Pop()" (typeToCsString typeMap outerType) (typeToName outerType)
//             write 2 "{"
//             for i, type_ in tupleTypes |> List.indexed do
//                 popValue writer 3 (ArrayType type_) (NewVariable (sprintf "_%dvalues" i))
//             write 3 "var count = _0values.Length;"
//             write 3 "var ret = new %s[count];" (typeToCsString typeMap elemType)
//             write 3 "for (var i = 0; i < count; i++)"
//             write 3 "{"
//             for i, _ in tupleTypes |> List.indexed do
//                 write 4 "var _%d = _%dvalues[i];" i i
//             let joinedVars =
//                 tupleTypes |> List.indexed |> List.map (fun (i, _) -> sprintf "_%d" i) |> String.concat ", "
//             write 4 "ret[i] = (%s);" joinedVars
//             write 3 "}"
//             write 3 "return ret;"
//             write 2 "}"
//         member this.ArrayType () =
//             arrayDefault()
//         member this.MapType () =
//             arrayDefault()
//         member this.DelegateType () =
//             arrayDefault()
//         member this.SetType () =
//             arrayDefault()
//         member this.SumType () =
//             arrayDefault()
//         member this.Primitive (prim: NIPrimitive) =
//             write 2 "// built-in array type: %s" (typeToCsString typeMap outerType)
//         member this.Options () =
//             arrayDefault()
//         member this.Opaque () =
//             // push
//             write 2 "internal static void __%s__Push(%s items)" (typeToName outerType) (typeToCsString typeMap outerType)
//             write 2 "{"
//             write 3 "var ptrs = items.Select(item => item.NativeHandle).ToArray();"
//             write 3 "NativeImplClient.PushPtrArray(ptrs);"
//             write 2 "}"
//             // pop
//             write 2 "internal static %s __%s__Pop()" (typeToCsString typeMap outerType) (typeToName outerType)
//             write 2 "{"
//             write 3 "return NativeImplClient.PopPtrArray()"
//             write 4 ".Select(ptr => ptr != IntPtr.Zero ? new %s(ptr) : null)" (typeToCsString typeMap elemType)
//             write 4 ".ToArray();"
//             write 2 "}"
//         member this.Other (t: NIType) =
//             write 2 "// TODO: push/pop for array of %s" (typeToCsString typeMap outerType)
//
// type SetRenderer(writer: IFormatWriter, typeMap: Map<string, UserOrGeneric>, outerType: NIType, elemType: NIType) =
//     let write, blank = writer.Write, writer.Blank
//     let setDefault () =
//         // use this for any element types with no particular specializations (typedefs, delegates, arrays -- everything but structs and tuples, basically)
//         // PUSH ========
//         write 2 "internal static void __%s__Push(%s values, bool isReturn)" (typeToName outerType) (typeToCsString typeMap outerType)
//         write 2 "{"
//         write 3 "foreach (var value in values)"
//         write 3 "{"
//         pushValue writer 4 elemType "value" (Indirect "isReturn")
//         write 3 "}"
//         write 3 "NativeImplClient.PushSizeT((IntPtr)values.Count);"
//         write 2 "}"
//         // POP =========
//         blank()
//         write 2 "internal static %s __%s__Pop()" (typeToCsString typeMap outerType) (typeToName outerType)
//         write 2 "{"
//         write 3 "var count = (int)NativeImplClient.PopSizeT();"
//         write 3 "var __ret = new %s();" (typeToCsString typeMap outerType)
//         write 3 "for (var i = 0; i < count; i++)"
//         write 3 "{"
//         popValue writer 4 elemType (NewVariable "item")
//         write 4 "__ret.Add(item);"
//         write 3 "}"
//         write 3 "return __ret;"
//         write 2 "}"
//     interface IArrayLikeRenderer with
//         member this.TypedefTuple (tt: NIType) =
//             // new, flattened outer (minus the typedef middleman)
//             let newOuter =
//                 ArrayType tt
//             // PUSH ====================
//             write 2 "internal static void __%s__Push(%s things, bool isReturn)" (typeToName newOuter) (typeToCsString typeMap newOuter)
//             write 2 "{"
//             write 3 "// ???"
//             write 2 "}"
//             // POP =====================
//             blank()
//             write 2 "internal static %s __%s__Pop()" (typeToCsString typeMap newOuter) (typeToName newOuter)
//             write 2 "{"
//             write 3 "// ???"
//             write 2 "}"
//         member this.EnumType (name: string) =
//             let widthType =
//                 match typeMap[name] with
//                 | UserOrGeneric.User (EnumDef info) ->
//                     let signed =
//                         not info.FlagsMode
//                     // what int size will we use to encode these?
//                     match info.MaybeWidth with
//                     | Some provided ->
//                         // use what the user asked for (currently no checking to verify it's big enough)
//                         provided
//                     | None ->
//                         // what's the smallest width that will accommodate the enum size?
//                         let iWidth =
//                             match info.Values.Length with
//                             | x when x < 128 -> Width8
//                             | x when x < 32768 -> Width16
//                             | _ -> Width32
//                         Primitive(Int(signed, iWidth))
//                 | _ -> failwith "SetRenderer this.EnumType trouble!!!"
//             // push
//             write 2 "internal static void __%s__Push(%s items, bool isReturn)" (typeToName outerType) (typeToCsString typeMap outerType)
//             write 2 "{"
//             write 3 "var intValues = items.Select(i => (%s)i).ToArray();" (typeToCsString typeMap widthType)
//             pushValue writer 3 (ArrayType widthType) "intValues" NotApplicable
//             write 2 "}"
//             // pop
//             blank()
//             write 2 "internal static %s __%s__Pop()" (typeToCsString typeMap outerType) (typeToName outerType)
//             write 2 "{"
//             popValue writer 3 (ArrayType widthType) (NewVariable "intValues")
//             write 3 "return intValues.Select(i => (%s)i).ToHashSet();" (typeToCsString typeMap elemType)
//             write 2 "}"
//         member this.StructType (structName: string) (namedFields: NamedField list) =
//             // PUSH ===================
//             write 2 "internal static void __%s__Push(%s items, bool isReturn)" (typeToName outerType) (typeToCsString typeMap outerType)
//             write 2 "{"
//             write 3 "// ???"
//             write 2 "}"
//             // POP ====================
//             blank()
//             write 2 "internal static %s __%s__Pop()" (typeToCsString typeMap outerType) (typeToName outerType)
//             write 2 "{"
//             write 3 "// ???"
//             write 2 "}"
//         member this.TupleType (tupleTypes: NIType list) =
//             write 2 "// IArrayRenderer.tupleType not yet handled (SetRenderer)"
//         member this.ArrayType () =
//             setDefault()
//         member this.MapType () =
//             setDefault()
//         member this.DelegateType () =
//             setDefault()
//         member this.SetType () =
//             setDefault()
//         member this.SumType () =
//             setDefault()
//         member this.Primitive (prim: NIPrimitive) =
//             // PUSH ==================
//             write 2 "internal static void __%s__Push(%s items, bool isReturn)" (typeToName outerType) (typeToCsString typeMap outerType)
//             write 2 "{"
//             pushValue writer 3 (ArrayType elemType) "items.ToArray()" (Indirect "isReturn")
//             write 2 "}"
//             // POP ===================
//             blank()
//             write 2 "internal static %s __%s__Pop()" (typeToCsString typeMap outerType) (typeToName outerType)
//             write 2 "{"
//             popValue writer 3 (ArrayType elemType) (NewVariable "items")
//             write 3 "return items.ToHashSet();"
//             write 2 "}"
//         member this.Opaque () =
//             // untested! haven't looked at this code in a long time, so just copied from above!
//             // PUSH ==================
//             write 2 "internal static void __%s__Push(%s items, bool isReturn)" (typeToName outerType) (typeToCsString typeMap outerType)
//             write 2 "{"
//             pushValue writer 3 (ArrayType elemType) "items.ToArray()" (Indirect "isReturn")
//             write 2 "}"
//             // POP ===================
//             blank()
//             write 2 "internal static %s __%s__Pop()" (typeToCsString typeMap outerType) (typeToName outerType)
//             write 2 "{"
//             popValue writer 3 (ArrayType elemType) (NewVariable "items")
//             write 3 "return items.ToHashSet();"
//             write 2 "}"
//         member this.Options() =
//             setDefault()
//         member this.Other (t: NIType) =
//             write 2 "// TODO: push/pop for set of %s" (typeToCsString typeMap outerType)
//
// let renderLibraryUserTypes (writer: IFormatWriter) (containsRefs: NIType -> bool) (name: string) (content: UserOrGeneric) (typeMap: Map<string, UserOrGeneric>) =
//     let write, blank = writer.Write, writer.Blank
//     //
//     blank()
//     match content with
//     | UserOrGeneric.FwdInterface ->
//         () // C# doesn't require forward declarations :)
//     | UserOrGeneric.Generic gType ->
//         match gType with
//         | BufferType elemType ->
//             write 2 "internal static void __%s__Push(%s buf, bool isReturn)" (typeToName gType) (typeToCsString typeMap gType)
//             write 2 "{"
//             write 3 "if (buf != null)"
//             write 3 "{"
//             write 4 "((IPushable)buf).Push(isReturn);"
//             write 3 "}"
//             write 3 "else"
//             write 3 "{"
//             write 4 "NativeImplClient.PushNull();"
//             write 3 "}"
//             write 2 "}"
//             // POP ==============
//             blank()
//             write 2 "internal static %s __%s__Pop()" (typeToCsString typeMap gType) (typeToName gType)
//             write 2 "{"
//             write 3 "NativeMethods.popBuffer(out var id, out var isClientId, out var bufferDescriptor);"
//             write 3 "if (id != 0)"
//             write 3 "{"
//             write 4 "if (isClientId)"
//             write 4 "{"
//             write 5 "return ClientBuffer<%s>.GetById(id);" (typeToCsString typeMap elemType)
//             write 4 "}"
//             write 4 "else"
//             write 4 "{"
//             write 5 "return new ServerBuffer<%s>(id, bufferDescriptor);" (typeToCsString typeMap elemType)
//             write 4 "}"
//             write 3 "}"
//             write 3 "else"
//             write 3 "{"
//             write 4 "return null;"
//             write 3 "}"
//             write 2 "}"
//         | TupleType tupleTypes ->
//             let typesJoined =
//                 tupleTypes |> List.map (typeToCsString typeMap) |> String.concat ", "
//             // PUSH =============
//             write 2 "internal static void __%s__Push((%s) value, bool isReturn)" (typeToName gType) typesJoined
//             write 2 "{"
//             for i, type_ in tupleTypes |> List.indexed |> List.rev do
//                 pushValue writer 3 type_ (sprintf "value.Item%d" (i+1)) (Indirect "isReturn")
//             write 2 "}"
//             // POP ==============
//             blank()
//             write 2 "internal static (%s) __%s__Pop()" typesJoined (typeToName gType)
//             write 2 "{"
//             for i, type_ in tupleTypes |> List.indexed do
//                 popValue writer 3 type_ (NewVariable (sprintf "_%d" i))
//             let varsJoined =
//                 tupleTypes |> List.indexed |> List.map (fun (i, _) -> sprintf "_%d" i) |> String.concat ", "
//             write 3 "return (%s);" varsJoined
//             write 2 "}"
//         | ArrayType elemType ->
//             let renderer =
//                 ArrayRenderer(writer, typeMap, gType, elemType)
//             renderArrayLike typeMap elemType renderer
//         | SetType elemType ->
//             let renderer =
//                 SetRenderer(writer, typeMap, gType, elemType)
//             renderArrayLike typeMap elemType renderer
//         | MapType (keyType, valueType) ->
//             // push
//             write 2 "internal static void __%s__Push(Dictionary<%s, %s> map, bool isReturn)" (typeToName gType) (typeToCsString typeMap keyType) (typeToCsString typeMap valueType)
//             write 2 "{"
//             pushValue writer 3 (ArrayType valueType) "map.Values.ToArray()" (Indirect "isReturn")
//             pushValue writer 3 (ArrayType keyType) "map.Keys.ToArray()" (Indirect "isReturn")
//             write 2 "}"
//             // pop
//             blank()
//             write 2 "internal static Dictionary<%s, %s> __%s__Pop()" (typeToCsString typeMap keyType) (typeToCsString typeMap valueType) (typeToName gType)
//             write 2 "{"
//             popValue writer 3 (ArrayType keyType) (NewVariable "keys")
//             popValue writer 3 (ArrayType valueType) (NewVariable "values")
//             write 3 "var ret = new Dictionary<%s,%s>();" (typeToCsString typeMap keyType) (typeToCsString typeMap valueType)
//             write 3 "for (var i = 0; i < keys.Length; i++)"
//             write 3 "{"
//             write 4 "ret[keys[i]] = values[i];"
//             write 3 "}"
//             write 3 "return ret;"
//             write 2 "}"
//         | _ ->
//             write 2 "// TODO: renderLibraryUserType: generic %s" (typeToCsString typeMap gType)
//     | UserOrGeneric.User userType ->
//         match userType with
//         | OpaqueDef info ->
//             let maybeInheritsFrom =
//                 match info.MaybeParent with
//                 | Some (Resolved(resolvedName, OpaqueType)) ->
//                     match resolvedName with
//                     | ThisModule name ->
//                         Some name
//                     | Imported(_, name) ->
//                         failwithf "opaques don't yet support cross-module inheritance (tried to inherit %s)" name
//                 | _ ->
//                     None
//             // class definition
//             match maybeInheritsFrom with
//             | Some parentName ->
//                 write 2 "public class %s : %s" (upperFirst name) (upperFirst parentName)
//                 write 2 "{"
//                 // no native handle required
//             | None ->
//                 write 2 "public class %s : IDisposable" (upperFirst name)
//                 write 2 "{"
//                 write 3 "internal readonly IntPtr NativeHandle;"
//                 write 3 "protected bool _disposed;"
//             // ctor
//             match maybeInheritsFrom with
//             | None ->
//                 // normal ctor
//                 write 3 "internal %s(IntPtr nativeHandle)" (upperFirst name)
//                 write 3 "{"
//                 write 4 "NativeHandle = nativeHandle;"
//                 write 3 "}"
//             | Some _ ->
//                 // inherited
//                 write 3 "internal %s(IntPtr nativeHandle) : base(nativeHandle)" (upperFirst name)
//                 write 3 "{"
//                 write 3 "}"
//             // dispose
//             let overrideOrVirtual =
//                 match maybeInheritsFrom with
//                 | Some _ -> "override"
//                 | None -> "virtual"
//             // TODO: we get this C# linting error, look into it:
//             // CA1816: Change Drawing.MutableAttributedString.Dispose() to call GC.SuppressFinalize(object). This will prevent derived types that introduce a finalizer from needing to re-implement 'IDisposable' to call it.
//             // note that every opaque type has its own _dispose method on the server, so we intentionally don't call the parent type's .Dispose() here
//             write 3 "public %s void Dispose()" overrideOrVirtual
//             write 3 "{"
//             write 4 "if (!_disposed)"
//             write 4 "{"
//             pushValue writer 5 (Resolved(ThisModule name,OpaqueType)) "this" (Direct false)
//             write 5 "NativeImplClient.InvokeModuleMethod(_%s_dispose);" name
//             write 5 "_disposed = true;"
//             write 4 "}"
//             write 3 "}"
//             // static methods
//             let staticMethods =
//                 info.StaticDefs
//                 |> List.choose (function
//                     | FunctionDef namedFunction ->
//                         Some namedFunction
//                     | _ ->
//                         None)
//             for namedFunc in staticMethods do
//                 let throwsComment =
//                     if namedFunc.Throws.IsEmpty then
//                         ""
//                     else
//                         let joined =
//                             namedFunc.Throws
//                             |> List.map (typeToCsString typeMap)
//                             |> String.concat ", "
//                         sprintf " // throws %s" joined
//                 write 3 "public static %s %s(%s)%s" (typeToCsString typeMap namedFunc.Returns) (upperFirst namedFunc.Name) (argsJoined typeMap namedFunc.Args) throwsComment
//                 write 3 "{"
//                 // push args in reverse
//                 for argName, argType in namedFunc.Args |> List.rev do
//                     pushValue writer 4 argType argName (Direct false)
//                 // invoke remote
//                 if namedFunc.Throws.IsEmpty then
//                     write 4 "NativeImplClient.InvokeModuleMethod(_%s_%s);" (lowerFirst name) namedFunc.Name
//                 else
//                     write 4 "NativeImplClient.InvokeModuleMethodWithExceptions(_%s_%s);" (lowerFirst name) namedFunc.Name
//                 // return pop
//                 popValue writer 4 namedFunc.Returns (if containsRefs namedFunc.Returns then SafeReturn else Return)
//                 write 3 "}"
//             // instance methods
//             for namedFunc in info.Methods do
//                 let throwsComment =
//                     if namedFunc.Throws.IsEmpty then
//                         ""
//                     else
//                         let joined =
//                             namedFunc.Throws
//                             |> List.map (typeToCsString typeMap)
//                             |> String.concat ", "
//                         sprintf " // throws %s" joined
//                 write 3 "public %s %s(%s)%s" (typeToCsString typeMap namedFunc.Returns) (upperFirst namedFunc.Name) (argsJoined typeMap namedFunc.Args) throwsComment
//                 write 3 "{"
//                 // push args in reverse
//                 for argName, argType in namedFunc.Args |> List.rev do
//                     pushValue writer 4 argType argName (Direct false)
//                 // push self argument
//                 let thisType =
//                     Resolved(ThisModule name, OpaqueType)
//                 pushValue writer 4 thisType "this" (Direct false)
//                 // invoke remote
//                 if namedFunc.Throws.IsEmpty then
//                     write 4 "NativeImplClient.InvokeModuleMethod(_%s_%s);" (lowerFirst name) namedFunc.Name
//                 else
//                     write 4 "NativeImplClient.InvokeModuleMethodWithExceptions(_%s_%s);" (lowerFirst name) namedFunc.Name
//                 // return pop
//                 popValue writer 4 namedFunc.Returns (if containsRefs namedFunc.Returns then SafeReturn else Return)
//                 write 3 "}"
//             write 2 "}"
//             // push
//             blank()
//             write 2 "[MethodImpl(MethodImplOptions.AggressiveInlining)]"
//             write 2 "internal static void %s__Push(%s thing)" (upperFirst name) (upperFirst name)
//             write 2 "{"
//             write 3 "NativeImplClient.PushPtr(thing?.NativeHandle ?? IntPtr.Zero);"
//             write 2 "}"
//             // pop
//             blank()
//             write 2 "[MethodImpl(MethodImplOptions.AggressiveInlining)]"
//             write 2 "internal static %s %s__Pop()" (upperFirst name) (upperFirst name)
//             write 2 "{"
//             write 3 "var ptr = NativeImplClient.PopPtr();"
//             write 3 "return ptr != IntPtr.Zero ? new %s(ptr) : null;" (upperFirst name)
//             write 2 "}"
//         | StructDef namedFields ->
//             write 2 "public struct %s {" name
//             for field in namedFields do
//                 write 3 "public %s %s;" (typeToCsString typeMap field.Type_) (upperFirst field.Name)
//             // constructor
//             write 3 "public %s(%s)" name (fieldsJoined typeMap namedFields)
//             write 3 "{"
//             for field in namedFields do
//                 write 4 "this.%s = %s;" (upperFirst field.Name) field.Name
//             write 3 "}"
//             write 2 "}"
//             // push
//             blank()
//             write 2 "internal static void %s__Push(%s value, bool isReturn)" (upperFirst name) (upperFirst name)
//             write 2 "{"
//             for field in namedFields |> List.rev do
//                 pushValue writer 3 field.Type_ (sprintf "value.%s" (upperFirst field.Name)) (Indirect "isReturn")
//             write 2 "}"
//             // pop
//             blank()
//             write 2 "internal static %s %s__Pop()" (upperFirst name) (upperFirst name)
//             write 2 "{"
//             for field in namedFields do
//                 popValue writer 3 field.Type_ (NewVariable field.Name)
//             write 3 "return new %s(%s);" name (fieldNamesJoined namedFields)
//             write 2 "}"
//         | EnumDef info ->
//             let widthType, widthStr =
//                 match info.MaybeWidth with
//                 | Some widthType ->
//                     widthType, sprintf " : %s" (typeToCsString typeMap widthType)
//                 | None ->
//                     let signed =
//                         not info.FlagsMode
//                     Primitive(Int(signed,Width32)), ""
//             if info.FlagsMode then
//                 write 2 "[Flags]"
//             write 2 "public enum %s%s" name widthStr
//             write 2 "{"
//             for (value, maybeSpecifiedValue), maybeComma in info.Values |> withTrailingCommas do
//                 match maybeSpecifiedValue with
//                 | Some specValue ->
//                     let enumValueStr =
//                         match specValue with
//                         | EnumValue.Decimal(value, leftShift) ->
//                             if leftShift > 0 then
//                                 sprintf "%d << %d" value leftShift
//                             else
//                                 sprintf "%d" value
//                         | EnumValue.Hex(value, leftShift) ->
//                             if leftShift > 0 then
//                                 sprintf "0x%X << %d" value leftShift
//                             else
//                                 sprintf "0x%X" value
//                     write 3 "%s = %s%s" (upperFirst value) enumValueStr maybeComma
//                 | None ->
//                     write 3 "%s%s" (upperFirst value) maybeComma
//             write 2 "}"
//             blank()
//             write 2 "[MethodImpl(MethodImplOptions.AggressiveInlining)]"
//             write 2 "internal static void %s__Push(%s value)" (upperFirst name) (upperFirst name)
//             write 2 "{"
//             let content =
//                 sprintf "(%s)value" (typeToCsString typeMap widthType)
//             pushValue writer 3 widthType content NotApplicable
//             write 2 "}"
//             blank()
//             write 2 "[MethodImpl(MethodImplOptions.AggressiveInlining)]"
//             write 2 "internal static %s %s__Pop()" (upperFirst name) (upperFirst name)
//             write 2 "{"
//             popValue writer 3 widthType (NewVariable "ret")
//             write 3 "return (%s)ret;" (upperFirst name)
//             write 2 "}"
//         | OptionsDef namedFields ->
//             // definition
//             write 2 "public struct %s" name
//             write 2 "{"
//             write 3 "[Flags]"
//             write 3 "internal enum Fields"
//             write 3 "{"
//             for (i, field), maybeComma in namedFields |> List.indexed |> withTrailingCommas do
//                 write 4 "%s = %d%s" (upperFirst field.Name) (1 <<< i) maybeComma
//             write 3 "}"
//             write 3 "internal Fields UsedFields;"
//             blank()
//             for field in namedFields do
//                 let fieldTypeCsString =
//                     (typeToCsString typeMap field.Type_)
//                 write 3 "private %s _%s;" fieldTypeCsString field.Name
//                 write 3 "public %s %s" fieldTypeCsString (upperFirst field.Name)
//                 write 3 "{"
//                 write 4 "set"
//                 write 4 "{"
//                 write 5 "_%s = value;" field.Name
//                 write 5 "UsedFields |= Fields.%s;" (upperFirst field.Name)
//                 write 4 "}"
//                 write 3 "}"
//                 write 3 "public readonly bool Has%s(out %s value)" (upperFirst field.Name) fieldTypeCsString
//                 write 3 "{"
//                 write 4 "if (UsedFields.HasFlag(Fields.%s))" (upperFirst field.Name)
//                 write 4 "{"
//                 write 5 "value = _%s;" field.Name
//                 write 5 "return true;"
//                 write 4 "}"
//                 write 4 "value = default;"
//                 write 4 "return false;"
//                 write 3 "}"
//             write 2 "}"
//             // push
//             write 2 "internal static void %s__Push(%s value, bool isReturn)" (upperFirst name) (upperFirst name)
//             write 2 "{"
//             for field in namedFields |> List.rev do
//                 write 3 "if (value.Has%s(out var %s))" (upperFirst field.Name) field.Name
//                 write 3 "{"
//                 pushValue writer 4 field.Type_ field.Name (Indirect "isReturn")
//                 write 3 "}"
//             pushValue writer 3 (Primitive(Int(true, Width32))) "(int)value.UsedFields" NotApplicable
//             write 2 "}"
//             // pop
//             write 2 "internal static %s %s__Pop()" (upperFirst name) (upperFirst name)
//             write 2 "{"
//             write 3 "var opts = new %s" (upperFirst name)
//             write 3 "{"
//             write 4 "UsedFields = (%s.Fields)NativeImplClient.PopInt32()" (upperFirst name) // hardcoded int32, I don't think we have anything for casting yet
//             write 3 "};"
//             for field in namedFields do
//                 write 3 "if (opts.UsedFields.HasFlag(%s.Fields.%s))" (upperFirst name) (upperFirst field.Name)
//                 write 3 "{"
//                 popValue writer 4 field.Type_ (ExistingVar $"opts.{upperFirst field.Name}")
//                 write 3 "}"
//             write 3 "return opts;"
//             write 2 "}"
//         | SumTypeDef sumTypeValues ->
//             // definition
//             write 2 "public abstract record %s" (upperFirst name)
//             write 2 "{"
//             for value in sumTypeValues do
//                 if value.Args.IsEmpty then
//                     write 3 "public sealed record %s : %s;" (upperFirst value.Name) name
//                 else
//                     let upperFirstArgs =
//                         value.Args |> List.map (fun (name, t) -> (upperFirst name), t)
//                     write 3 "public sealed record %s(%s) : %s" (upperFirst value.Name) (argsJoined typeMap upperFirstArgs) name
//                     write 3 "{"
//                     for argName, argType in upperFirstArgs do
//                         write 4 "public %s %s { get; } = %s;" (typeToCsString typeMap argType) argName argName
//                     write 3 "}"
//             write 2 "}"
//             // tags for push/pop
//             blank()
//             write 2 "private enum %s__Tag" (upperFirst name)
//             write 2 "{"
//             for value, maybeComma in sumTypeValues |> withTrailingCommas do
//                 write 3 "%s%s" (upperFirst value.Name) maybeComma
//             write 2 "}"
//             let tagType =
//                 Primitive(Int(true,Width32))
//             // PUSH
//             blank()
//             write 2 "internal static void %s__Push(%s thing, bool isReturn)" (upperFirst name) (upperFirst name)
//             write 2 "{"
//             write 3 "%s__Tag which;" (upperFirst name)
//             write 3 "switch (thing)"
//             write 3 "{"
//             for value in sumTypeValues do
//                 write 4 "case %s.%s %s:" (upperFirst name) (upperFirst value.Name) (lowerFirst value.Name)
//                 write 5 "which = %s__Tag.%s;" (upperFirst name) (upperFirst value.Name)
//                 for argName, argType in value.Args |> List.rev do
//                     pushValue writer 5 argType (sprintf "%s.%s" (lowerFirst value.Name) (upperFirst argName)) (Indirect "isReturn")
//                 write 5 "break;"
//             write 4 "default:"
//             write 5 "throw new ArgumentOutOfRangeException(nameof(thing));"
//             write 3 "}"
//             pushValue writer 3 tagType "(int)which" NotApplicable
//             write 2 "}"
//             // POP
//             blank()
//             write 2 "internal static %s %s__Pop()" (upperFirst name) (upperFirst name)
//             write 2 "{"
//             popValue writer 3 tagType (NewVariable "which")
//             write 3 "switch ((%s__Tag)which)" (upperFirst name)
//             write 3 "{"
//             for value in sumTypeValues do
//                 write 4 "case %s__Tag.%s:" (upperFirst name) (upperFirst value.Name)
//                 write 4 "{"
//                 for argName, argType in value.Args do
//                     popValue writer 5 argType (NewVariable argName)
//                 write 5 "return new %s.%s(%s);" (upperFirst name) (upperFirst value.Name) (argNamesJoined value.Args)
//                 write 4 "}"
//             write 4 "default:"
//             write 5 "throw new ArgumentOutOfRangeException();"
//             write 3 "}"
//             write 2 "}"
//         | TypeDef niType ->
//             // C# has no proper typedefs, so we just emit push/pop that pass through to the real thing
//             // (at least it will make wrapper funcs a bit clearer)
//             // PUSH
//             write 2 "[MethodImpl(MethodImplOptions.AggressiveInlining)]"
//             write 2 "internal static void %s__Push(%s value, bool isReturn)" name (typeToCsString typeMap niType)
//             write 2 "{"
//             pushValue writer 3 niType "value" (Indirect "isReturn")
//             write 2 "}"
//             // POP
//             blank()
//             write 2 "[MethodImpl(MethodImplOptions.AggressiveInlining)]"
//             write 2 "internal static %s %s__Pop()" (typeToCsString typeMap niType) name
//             write 2 "{"
//             popValue writer 3 niType Return
//             write 2 "}"
//         | InterfaceDef namedFunctions ->
//             blank()
//             // raw interface itself
//             write 2 "public interface %s : IDisposable" (upperFirst name)
//             write 2 "{"
//             for method in namedFunctions do
//                 let throwsComment =
//                     if method.Throws.IsEmpty then
//                         ""
//                     else
//                         let joined =
//                             method.Throws
//                             |> List.map (typeToCsString typeMap)
//                             |> String.concat ", "
//                         sprintf " // throws %s" joined
//                 write 3 "%s %s(%s);%s" (typeToCsString typeMap method.Returns) (upperFirst method.Name) (argsJoined typeMap method.Args) throwsComment
//             write 2 "}"
//             // push
//             blank()
//             write 2 "internal static void %s__Push(%s thing, bool isReturn)" (upperFirst name) (upperFirst name)
//             write 2 "{"
//             write 3 "if (thing != null)"
//             write 3 "{"
//             write 4 "((IPushable)thing).Push(isReturn);"
//             write 3 "}"
//             write 3 "else"
//             write 3 "{"
//             write 4 "NativeImplClient.PushNull();"
//             write 3 "}"
//             write 2 "}"
//             // pop
//             blank()
//             write 2 "internal static %s %s__Pop()" (upperFirst name) (upperFirst name)
//             write 2 "{"
//             write 3 "NativeImplClient.PopInstanceId(out var id, out var isClientId);"
//             write 3 "if (id != 0)"
//             write 3 "{"
//             write 4 "if (!isClientId)"
//             write 4 "{"
//             write 5 "return new Server%s(id);" (upperFirst name)
//             write 4 "}"
//             write 4 "else"
//             write 4 "{"
//             write 5 "return (%s) ClientObject.GetById(id);" (upperFirst name)
//             write 4 "}"
//             write 3 "}"
//             write 3 "else"
//             write 3 "{"
//             write 4 "return null;"
//             write 3 "}"
//             write 2 "}"
//             // client base class
//             blank()
//             write 2 "public abstract class Client%s : ClientObject, %s" (upperFirst name) (upperFirst name)
//             write 2 "{"
//             write 3 "public virtual void Dispose()"
//             write 3 "{"
//             write 4 "// override if necessary"
//             write 3 "}"
//             // abstract methods for client base class
//             for method in namedFunctions do
//                 let throwsComment =
//                     if method.Throws.IsEmpty then
//                         ""
//                     else
//                         let joined =
//                             method.Throws
//                             |> List.map (typeToCsString typeMap)
//                             |> String.concat ", "
//                         sprintf " // throws %s" joined
//                 write 3 "public abstract %s %s(%s);%s" (typeToCsString typeMap method.Returns) (upperFirst method.Name) (argsJoined typeMap method.Args) throwsComment
//             write 2 "}"
//             // server wrapper
//             blank()
//             write 2 "internal class Server%s : ServerObject, %s" (upperFirst name) (upperFirst name)
//             write 2 "{"
//             write 3 "public Server%s(int id) : base(id)" name
//             write 3 "{"
//             write 3 "}"
//             // server wrapper methods
//             for method in namedFunctions do
//                 blank()
//                 write 3 "public %s %s(%s)" (typeToCsString typeMap method.Returns) (upperFirst method.Name) (argsJoined typeMap method.Args)
//                 write 3 "{"
//                 // push args (in reverse!!)
//                 for argName, argType in method.Args |> List.rev do
//                     pushValue writer 4 argType argName (Direct false)
//                 // invoke remote
//                 if method.Throws.IsEmpty then
//                     write 4 "NativeImplClient.InvokeInterfaceMethod(_%s_%s, Id);" (lowerFirst name) method.Name
//                 else
//                     write 4 "NativeImplClient.InvokeInterfaceMethodWithExceptions(_%s_%s, Id);" (lowerFirst name) method.Name
//                 // return (mindful of safety area nonsense)
//                 if containsRefs method.Returns then
//                     popValue writer 4 method.Returns SafeReturn
//                 else
//                     popValue writer 4 method.Returns Return
//                 write 3 "}"
//             blank()
//             write 3 "public void Dispose()"
//             write 3 "{"
//             write 4 "ServerDispose();"
//             write 3 "}"
//             write 2 "}"
//         | ExceptionDef fields ->
//             write 2 "public class %s : Exception" name
//             write 2 "{"
//             for f in fields do
//                 write 3 "public readonly %s %s;" (typeToCsString typeMap f.Type_) (upperFirst f.Name)
//             write 3 "public %s(%s) : base(\"%s\")" name (fieldsJoined typeMap fields) name
//             write 3 "{"
//             for f in fields do
//                 write 4 "%s = %s;" (upperFirst f.Name) f.Name
//             write 3 "}"
//             write 3 "internal void PushAndSet()"
//             write 3 "{"
//             for f in fields |> List.rev do
//                 pushValue writer 4 f.Type_ (upperFirst f.Name) (Direct true) // true since pushed exceptions are basically returns, any reference fields need to be safely pushed
//             write 4 "NativeImplClient.SetException(_%s);" (lowerFirst name)
//             write 3 "}"
//             write 3 "internal static void BuildAndThrow()"
//             write 3 "{"
//             for f in fields do
//                 popValue writer 4 f.Type_ (NewVariable f.Name)
//             let namesJoined =
//                 [for f in fields -> f.Name] |> String.concat ", "
//             write 4 "throw new %s(%s);" name namesJoined
//             write 3 "}"
//             write 2 "}"
//         | DelegateDef info ->
//             write 2 "public delegate %s %s(%s);" (typeToCsString typeMap info.Returns) name (argsJoined typeMap info.Args)
//             // push
//             blank()
//             write 2 "internal static void %s__Push(%s callback)" name name
//             write 2 "{"
//             write 3 "void CallbackWrapper()"
//             write 3 "{"
//             for argName, argType in info.Args do
//                 popValue writer 4 argType (NewVariable argName)
//             let invocation =
//                 sprintf "callback(%s)" (argNamesJoined info.Args)
//             if info.Throws.IsEmpty then
//                 pushValue writer 4 info.Returns invocation (Direct true)
//             else
//                 write 4 "try"
//                 write 4 "{"
//                 pushValue writer 5 info.Returns invocation (Direct true)
//                 write 4 "}"
//                 for exc in info.Throws do
//                     match exc with
//                     | Resolved (resolvedName, ExceptionType) as exc ->
//                         let exceptionName =
//                             resolvedName.Flatten()
//                         write 4 "catch (%s e)" (upperFirst exceptionName)
//                         write 4 "{"
//                         write 5 "e.PushAndSet();"
//                         write 4 "}"
//                     | _ ->
//                         failwithf "C# writing delegate: 'throws' contained non-exception type"
//             write 3 "}"
//             write 3 "NativeImplClient.PushClientFuncVal(CallbackWrapper, Marshal.GetFunctionPointerForDelegate(callback));"
//             write 2 "}"
//             // pop
//             blank()
//             write 2 "internal static %s %s__Pop()" name name
//             write 2 "{"
//             write 3 "var id = NativeImplClient.PopServerFuncValId();"
//             write 3 "var remoteFunc = new ServerFuncVal(id);"
//             write 3 "%s Wrapper(%s)" (typeToCsString typeMap info.Returns) (argsJoined typeMap info.Args)
//             write 3 "{"
//             for argName, argType in info.Args |> List.rev do
//                 pushValue writer 4 argType argName (Direct false)
//             if info.Throws.IsEmpty then
//                 write 4 "remoteFunc.Exec();"
//             else
//                 write 4 "remoteFunc.ExecWithExceptions();"
//             popValue writer 4 info.Returns Return
//             write 3 "}"
//             write 3 "return Wrapper;"
//             write 2 "}"

let joinModuleName (ModuleName nameParts) =
    joinNameParts nameParts

let writeModule (context: ModuleContext) (writer: IFormatWriter) (baseNamespace: string) (maybeModulePrefix: string option) (className: string) =
    let write, blank = writer.Write, writer.Blank
    
    let m =
        context.Module

    let moduleMethods =
        m.Defs
        |> List.choose (function
            | FunctionDef namedFunction -> Some namedFunction
            | _ -> None)
    let opaques =
        m.Defs
        |> List.choose (function
            | UserType (name, OpaqueDef methods) ->
                Some (name, methods)
            | _ ->
                None)
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
    write 0 "using %s.Support;" baseNamespace
    write 0 "using ModuleHandle = %s.Support.ModuleHandle;" baseNamespace // sigh
    
    // imports
    if not m.Imports.IsEmpty then
        write 0 ""
        for importName in m.Imports do
            write 0 "using static %s.%s;" baseNamespace (joinModuleName importName)

    write 0 ""
    let moduleNamespace =
        maybeModulePrefix
        |> Option.map (fun mp -> sprintf "%s.%s" baseNamespace mp)
        |> Option.defaultValue baseNamespace
    write 0 "namespace %s" moduleNamespace
    write 0 "{"
    write 1 "public static class %s" className
    write 1 "{"
    write 2 "private static ModuleHandle _module;"

    // let typeMap =
    //     userTypesAndGenerics m
    
    // top level functions
    for namedFunc in moduleMethods do
        write 2 "private static ModuleMethodHandle _%s;" namedFunc.Name
        
    // opaque methods
    for name, info in opaques do
        // instance methods
        for method in info.Methods do
            write 2 "private static ModuleMethodHandle _%s_%s;" (lowerFirst name) method.Name
        // synthesized dispose method
        write 2 "private static ModuleMethodHandle _%s_dispose;" name
    
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
        write 2 "public static %s %s { get; private set; }" (typeToCsString context type_) (upperFirst name)
    
    // user types ====================
    // let containsRefsMap =
    //     createContainsRefsMap typeMap
    // let containsRefs (type_: NIType) =
    //     containsRefsMap.TryFind(typeToInternalKey type_)
    //     |> Option.defaultValue false
    // let renderFunc =
    //     renderLibraryUserTypes writer containsRefs
    // renderUserTypes typeMap renderFunc
    
    // top-level functions ===========
    for namedFunc in moduleMethods do
        blank()
        write 2 "public static %s %s(%s)" (typeToCsString context namedFunc.Returns) (upperFirst namedFunc.Name) (argsJoined context namedFunc.Args)
        write 2 "{"
        // push args in reverse
        for argName, argType in namedFunc.Args |> List.rev do
            pushValue writer 3 argType argName (Direct false)
        // invoke remote
        if namedFunc.Throws.IsEmpty then
            write 3 "NativeImplClient.InvokeModuleMethod(_%s);" namedFunc.Name
        else
            write 3 "NativeImplClient.InvokeModuleMethodWithExceptions(_%s);" namedFunc.Name
        // return pop
        popValue writer 3 namedFunc.Returns (if containsRefs context namedFunc.Returns then SafeReturn else Return)
        write 2 "}"
        
    // static init + shutdown ====================================================
    let maybeModuleInit =
        moduleMethods
        |> List.tryFind (fun nf -> nf.Name = "moduleInit")
    let maybeModuleShutdown =
        moduleMethods
        |> List.tryFind (fun nf -> nf.Name = "moduleShutdown")
    blank()
    match maybeModuleInit with
    | Some initFunc ->
        write 2 "internal static %s Init(%s)" (typeToCsString context initFunc.Returns) (argsJoined context initFunc.Args)
    | None ->
        write 2 "internal static void Init()"
    write 2 "{"
    write 3 "_module = NativeImplClient.GetModule(\"%s\");" (joinModuleName m.Name)
    
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
            
    // opaque method handles
    if not opaques.IsEmpty then
        blank()
        for name, info in opaques do
            // instance methods
            for method in info.Methods do
                write 3 "_%s_%s = NativeImplClient.GetModuleMethod(_module, \"%s_%s\");" (lowerFirst name) method.Name name method.Name
            // synthesized dispose method handle
            write 3 "_%s_dispose = NativeImplClient.GetModuleMethod(_module, \"%s_dispose\");" name name
    
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
                write 3 "NativeImplClient.SetClientMethodWrapper(_%s_%s, delegate(ClientObject obj)" (lowerFirst ifaceName) method.Name
                write 3 "{"
                write 4 "var inst = (Client%s) obj;" ifaceName
                // pop args
                for argName, argType in method.Args do
                    popValue writer 4 argType (NewVariable argName)
                // exec + return
                let invocation =
                    sprintf "inst.%s(%s)" (upperFirst method.Name) (argNamesJoined method.Args)
                if method.Throws.IsEmpty then
                    pushValue writer 4 method.Returns invocation (Direct true)
                else
                    write 4 "try"
                    write 4 "{"
                    pushValue writer 5 method.Returns invocation (Direct true)
                    write 4 "}"
                    for exc in method.Throws do
                        match exc with
                        | Resolved (_, ExceptionType) ->
                            write 4 "catch (%s e)" (typeToCsString context exc)
                            write 4 "{"
                            write 5 "NativeImplClient.SetException(_%s);" (lowerFirst (typeToCsString context exc))
                            write 5 "e.PushAndSet();"
                            write 4 "}"
                        | _ ->
                            failwith "C# interface wrappers: 'throws' contained non-exception type"
                write 3 "});"
    
    // invoke user's ModuleInit, if it exists
    match maybeModuleInit with
    | Some initFunc ->
        blank()
        let maybeReturn =
            match initFunc.Returns with
            | UnitType ->
                ""
            | _ ->
                "return "
        write 3 "%sModuleInit(%s);" maybeReturn (argNamesJoined initFunc.Args)
    | None ->
        blank()
        write 3 "// no static init"
    write 2 "}" // end of Init()
    blank()
    write 2 "internal static void Shutdown()"
    write 2 "{"
    match maybeModuleShutdown with
    | Some _ ->
        write 3 "ModuleShutdown();"
    | None ->
        write 3 "// no static shutdown"
    write 2 "}"
    write 1 "}" // end of static class <module name>
    //
    write 0 "}" // end of namespace
    
let projectDir (outParams: OutParams) =
    sprintf "%s/client/csharp/%sClient" outParams.Path outParams.LibraryName
    
let generateModule (context: ModuleContext) (outParams: OutParams) =
    let baseNamespace, maybeModulePrefix, className =
        namespaceParts outParams context.Module.Name
    let moduleDir =
        let subPath =
            maybeModulePrefix
            |> Option.map (fun mp -> "/" + mp.Replace(".", "/"))
            |> Option.defaultValue ""
        sprintf "%s%s" (projectDir outParams) subPath
    Directory.CreateDirectory(moduleDir) |> ignore
    let filename =
        sprintf "%s/%s.cs" moduleDir className
    let modulePrefixString =
        maybeModulePrefix
        |> Option.map (fun mp -> $"{mp}.")
        |> Option.defaultValue ""
    printfn "C# writing (%s.%s%s) [%s]" baseNamespace modulePrefixString className filename
    use output =
        let writer = new StreamWriter(filename)
        new SimpleFormatWriter(writer)
    writeModule context output baseNamespace maybeModulePrefix className
    
let writeProject (writer: IFormatWriter) (outParams: OutParams) =
    let write, blank = writer.Write, writer.Blank
    write 0 "<Project Sdk=\"Microsoft.NET.Sdk\">"
    blank ()
    write 1 "<PropertyGroup>"
    write 2 "<TargetFramework>net7.0</TargetFramework>"
    write 2 "<ImplicitUsings>enable</ImplicitUsings>"
    write 2 "<AllowUnsafeBlocks>true</AllowUnsafeBlocks>"
    let rootNamespace =
        outParams.MaybeNamespace
        |> Option.map (fun ns -> $"{ns}.{outParams.LibraryName}")
        |> Option.defaultValue outParams.LibraryName
    write 2 "<RootNamespace>%s</RootNamespace>" rootNamespace
    write 1 "</PropertyGroup>"
    blank ()
    write 0 "</Project>"

let generateProject (outParams: OutParams) =
    let filename =
        sprintf "%s/%sClient.csproj" (projectDir outParams) outParams.LibraryName
    use output =
        let writer = new StreamWriter(filename)
        new SimpleFormatWriter(writer)
    writeProject output outParams
    
let writeLibrary (allModules: Module list) (writer: IFormatWriter) (baseNamespace: string) =
    let write, blank = writer.Write, writer.Blank

    write 0 "using %s.Support;" baseNamespace
    blank()
    write 0 "namespace %s;" baseNamespace
    blank()
    write 0 "public static class Library"
    write 0 "{"
    write 1 "public static void Init()"
    write 1 "{"
    write 2 "if (NativeImplClient.Init() != 0)"
    write 2 "{"
    write 3 "Console.WriteLine(\"NativeImplClient.Init failed\");"
    write 3 "return;"
    write 2 "}"
    write 2 "// registrations, static module inits"
    for m in allModules do
        write 2 "%s.Init();" (joinModuleName m.Name)
    write 1 "}"
    blank()
    write 1 "public static void Shutdown()"
    write 1 "{"
    write 2 "// module static shutdowns (if any, might be empty)"
    for m in allModules |> List.rev do
        write 2 "%s.Shutdown();" (joinModuleName m.Name)
    write 2 "// bye"
    write 2 "NativeImplClient.Shutdown();"
    write 1 "}"
    blank()
    write 1 "public static void DumpTables()"
    write 1 "{"
    write 2 "NativeImplClient.DumpTables();"
    write 1 "}"
    write 0 "}"

let generateLibraryFile (allModules: Module list) (outParams: OutParams) =
    let baseNamespace, maybeModulePrefix, className =
        namespaceParts outParams (ModuleName ["Library"])
    let moduleDir =
        let subPath =
            maybeModulePrefix
            |> Option.map (fun mp -> "/" + mp.Replace(".", "/"))
            |> Option.defaultValue ""
        sprintf "%s%s" (projectDir outParams) subPath
    Directory.CreateDirectory(moduleDir) |> ignore
    let filename =
        sprintf "%s/%s.cs" moduleDir className
    let modulePrefixString =
        maybeModulePrefix
        |> Option.map (fun mp -> $"{mp}.")
        |> Option.defaultValue ""
    printfn "C# writing (%s.%s%s) [%s]" baseNamespace modulePrefixString className filename
    use output =
        let writer = new StreamWriter(filename)
        new SimpleFormatWriter(writer)
    writeLibrary allModules output baseNamespace
