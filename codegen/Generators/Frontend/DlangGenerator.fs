module Generators.Frontend.DlangGenerator

open System.IO
open AppCommon
open Generators.Common
open Parser
open Util

let isReservedWord =
    let reservedWords =
        [ "align"
          "version" ]
        |> Set.ofList
    reservedWords.Contains

let safeIdentifier (raw: string) =
    if isReservedWord raw then
        raw + "_"
    else
        raw

type DlangRenderContext = {
    LibraryName: string
    RenderContext: RenderContext
} with
    member this.LibraryPrefix =
        [ this.LibraryName ]
        |> List.map lower
    // member this.BaseNamespaceJoined =
    //     joinNameParts "." this.BaseNamespace
    member this.ModuleContext =
        this.RenderContext.ModuleContext
    member this.GroupContext =
        this.RenderContext.GroupContext
    member this.NavigateToSub (name: string) =
        let nextRenderContext =
            this.RenderContext.NavigateToSub name
        { this with RenderContext = nextRenderContext }
    member this.InStaticContext =
        this.RenderContext.GroupContext.KindPath
        |> List.exists (function
            | ModuleRoot -> false
            | NamedGroup -> true
            | OpaqueStatic -> true
            | InterfaceStatic -> true
            | StructStatic -> true)

let resolvedNameToString (context: DlangRenderContext) (resolvedName: ResolvedName) (mode: ResolvedNameStringMode) =
    let qualification =
        let prefix =
            match mode with
            | IdentifierMode ->
                [""]
            | TypeMode | WrapperMethodMode _ ->
                context.LibraryPrefix
            | GenericIdentifierMode ->
                // colliding with local symbols not a concern in this context, so we can use any (or probably even no) prefix
                ["G"]
            | EnumValueMode ->
                failwith "dlang resolvedNameToString: figure out how to handle EnumValueMode"
        Forced prefix
    let capsPolicy = {
        ModulePolicy = LowerOnly
    }
    resolvedNameToStringAbstract "." (fun name _ -> name) None qualification capsPolicy context.RenderContext resolvedName mode

let upperFirst =
    upperFirst Ignore

let typeToDlangString (context: DlangRenderContext) (outerType: NIType) =
    let rec recInner = function
        | UnitType -> "void"
        | Primitive prim ->
            match prim with
            | SizeT -> "size_t"
            | Bool -> "bool"
            | Int(signed, intWidth) ->
                match signed, intWidth with
                | true, Width8 -> "byte"
                | false, Width8 -> "ubyte"
                | true, Width16 -> "short"
                | false, Width16 -> "ushort"
                | true, Width32 -> "int"
                | false, Width32 -> "uint"
                | true, Width64 -> "long"
                | false, Width64 -> "ulong"
            | Float32 -> "float"
            | Float64 -> "double"
            | StringType -> "string"
        | Resolved (resolvedName, _) ->
            resolvedNameToString context resolvedName TypeMode
        | ArrayType elemType ->
            sprintf "%s[]" (recInner elemType)
        | SetType elemType ->
            sprintf "Set!(%s)" (recInner elemType)
        | BufferType elemType ->
            sprintf "ni.INativeBuffer!%s" (recInner elemType)
        | TupleType tupleTypes ->
            let joined =
                tupleTypes
                |> List.map recInner
                |> String.concat ", "
            sprintf "Tuple!(%s)" joined
        | MapType (keyType, valueType) ->
            sprintf "%s[%s]" (recInner valueType) (recInner keyType)
        | OptionType elemType ->
            sprintf "Nullable!(%s)" (recInner elemType)
        | ResultType (succType, errType) ->
            sprintf "Result!(%s, %s)" (recInner succType) (recInner errType)
        | x ->
            sprintf "(typeToDlangString: not yet supported: %A)" x
    // go
    recInner outerType

let typeToName (context: DlangRenderContext) (outerType: NIType) =
    let rec recInner (inGeneric: bool) = function
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
            resolvedNameToString context resolvedName (if inGeneric then GenericIdentifierMode else IdentifierMode)
        | TupleType tupleTypes ->
            let joined =
                tupleTypes
                |> List.map (recInner true)
                |> String.concat ""
            sprintf "%s_Tuple" joined
        | ArrayType niType ->
            sprintf "%s_Array" (recInner true niType)
        | SetType niType ->
            sprintf "%s_Set" (recInner true niType)
        | MapType (keyType, valueType) ->
            sprintf "%s_%s_Map" (recInner true keyType) (recInner true valueType)
        | OptionType elemType ->
            sprintf "%s_Option" (recInner true elemType)
        | ResultType (succType, errType) ->
            sprintf "%s_%s_Result" (recInner true succType) (recInner true errType)
        | BufferType elemType ->
            sprintf "Native_%s_Buffer" (recInner true elemType)
        | x -> sprintf "(typeToName: not yet supported: %A)" x
    recInner false outerType

let typeWithPrefix (context: DlangRenderContext) (outerType: NIType) (prefix: string) =
    // function for appropriately-qualified path to parent interface, but with <prefix> inserted just before the final part (eg org.whatever.<prefix>Thing)
    // (note, from C# generator, not yet tested :)
    let x = typeToDlangString context outerType
    let parts = x.Split(".")
    let lastIndex = parts.Length - 1
    parts[lastIndex] <- prefix + parts[lastIndex]
    String.concat "." parts

let joinedArgs (context: DlangRenderContext) (args: (string * NIType) list) =
    [for name, type_ in args -> sprintf "%s %s" (typeToDlangString context type_) (safeIdentifier name)]
    |> String.concat ", "

let constExprToString (context: DlangRenderContext) (forType: NIType option) =
    let config =
        { NullStr = "null"
          NoneStr =
              match forType with
              | Some (OptionType _ as niType) -> sprintf "%s()" (typeToDlangString context niType)
              | _ -> "!! default 'none' value but meaningless in this context !!"
          ScopeResStr = "." }
    let strFunc resolvedName =
        resolvedNameToString context resolvedName TypeMode
    let castFunc niType content =
        sprintf "cast(%s)(%s)" (typeToDlangString context niType) content
    typedConstExprString config strFunc castFunc

let joinedNameFuncArgs (context: DlangRenderContext) (args: NamedFunctionArg list) =
    [for arg in args ->
        match arg.DefaultValue with
        | Some value ->
            let typeStr =
                typeToDlangString context arg.Type
            let forceCast =
                // make sure to cast any constexprs when assigning to enums, UNLESS they have a top-level cast already
                match arg.Type with
                | Resolved (_, EnumType) ->
                    match value with
                    | Cast _ -> false
                    | _ -> true
                | _ -> false
            if forceCast then
                sprintf "%s %s = cast(%s) %s" typeStr (safeIdentifier arg.Name) typeStr (constExprToString context (Some arg.Type) value)
            else
                sprintf "%s %s = %s" typeStr (safeIdentifier arg.Name) (constExprToString context (Some arg.Type) value)
        | None ->
            sprintf "%s %s" (typeToDlangString context arg.Type) (safeIdentifier arg.Name) ]
    |> String.concat ", "

let joinedArgNames (args: (string * NIType) list) =
    [for name, _ in args -> (safeIdentifier name)]
    |> String.concat ", "

let joinedNamedFuncArgNames (args: NamedFunctionArg list) =
    [for arg in args -> (safeIdentifier arg.Name)]
    |> String.concat ", "

let fieldsJoined (context: DlangRenderContext) (fields: NamedField list) =
    [for f in fields -> sprintf "%s %s" (typeToDlangString context f.Type_) (safeIdentifier f.Name)]
    |> String.concat ", "

let fieldNamesJoined (fields: NamedField list) =
    [for f in fields -> (safeIdentifier f.Name)]
    |> String.concat ", "

type PopValueContext =
    | NewVariable of name: string
    | ExistingVar of name: string
    | Return
    | SafeReturn
    | WrapAndReturn of wrapFunc: (string -> string) // apply some text transformation to the raw pop content before returning

let popValue (context: DlangRenderContext) (writer: IFormatWriter) (level: int) (type_: NIType) (popContext: PopValueContext) =
    let write = writer.Write
    let raw =
        match type_ with
        | UnitType ->
            match popContext with
            | Return -> ""
            | _ -> failwith "tried to pop a Unit value in a non-Return context, why?"
        | Primitive prim ->
            match prim with
            | SizeT -> "ni.popSizeT()"
            | Bool -> "ni.popBool()"
            | Int(signed, width) ->
                sprintf "ni.pop%sInt%d()" (if signed then "" else "U") (widthToInt width)
            | Float32 -> "ni.popFloat()"
            | Float64 -> "ni.popDouble()"
            | StringType -> "ni.popString()"
        | Resolved (resolvedName, _) ->
            let name =
                // all other types:
                resolvedNameToString context resolvedName (WrapperMethodMode "__%s") // annoying way of preserving the necessary prefix to the type, while injecting a `__` just before the name (eg. module.prefix.NamedGroup.__Type)
            sprintf "%s__pop()" name
        | ArrayType niType ->
            match niType with
            | Primitive prim ->
                match prim with
                | Bool -> "ni.popBoolArray()"
                | Int(signed, width) ->
                    sprintf "ni.pop%sInt%dArray()" (if signed then "" else "U") (widthToInt width)
                | Float32 -> "ni.popFloatArray()"
                | Float64 -> "ni.popDoubleArray()"
                | StringType -> "ni.popStringArray()"
                | _ -> sprintf "/* popValue not yet supported: array of prim %A */" prim
            | Resolved _ ->
                sprintf "__%s__pop()" (typeToName context (ArrayType niType))
            | ArrayType _ -> sprintf "__%s__pop()" (typeToName context type_) // sigh, normalize all these already - doing it so many different ways...
            | SetType _ -> sprintf "__%s__pop()" (typeToName context type_)
            | MapType _ -> sprintf "__%s__pop()" (typeToName context type_)
            | TupleType _ -> sprintf "__%s__pop()" (typeToName context type_)
            | OptionType _ -> sprintf "__%s__pop()" (typeToName context type_)
            | ResultType _ -> sprintf "__%s__pop()" (typeToName context type_)
            | _ -> sprintf "/* popValue not yet supported: array of %A */" niType
        | MapType _ ->
            sprintf "__%s__pop()" (typeToName context type_)
        | TupleType _ ->
            sprintf "__%s__pop()" (typeToName context type_)
        | SetType _ ->
            sprintf "__%s__pop()" (typeToName context type_)
        | OptionType _ ->
            sprintf "__%s__pop()" (typeToName context type_)
        | ResultType _ ->
            sprintf "__%s__pop()" (typeToName context type_)
        | BufferType _ ->
            sprintf "__%s__pop()" (typeToName context type_)
        | _ ->
            sprintf "/* popValue %A not yet supported */" type_
    match popContext with
    | NewVariable name ->
        write level "auto %s = %s;" name raw
    | ExistingVar name ->
        write level "%s = %s;" name raw
    | Return ->
        match type_, popContext with
        | UnitType, Return ->
            // special case, don't write anything
            ()
        | _ ->
            write level "return %s;" raw
    | SafeReturn ->
        write level "auto __ret = %s;" raw
        write level "ni.clearServerSafetyArea();"
        write level "return __ret;"
    | WrapAndReturn wrapFunc ->
        let wrapped = wrapFunc(raw)
        write level "return %s;" wrapped

type PushIsReturn =
    | Direct of value: bool
    | Indirect of name: string
    | NotApplicable

let pushValue (context: DlangRenderContext) (writer: IFormatWriter) (level: int) (type_: NIType) (content: string) (isReturn: PushIsReturn) =
    let write = writer.Write
    let isReturnString() =
        match isReturn with
        | Direct value -> if value then "true" else "false"
        | Indirect name -> name
        | NotApplicable -> failwith "pushValue: 'NotApplicable' isReturn was consumed"
    let containsRefs =
        typeContainsRefs context.ModuleContext (NITypeRoot type_)
    match type_ with
    | UnitType ->
        write level "%s;" content // content directly (method invocation etc)
    | Primitive prim ->
        match prim with
        | SizeT -> write level "ni.pushSizeT(%s);" content
        | Bool -> write level "ni.pushBool(%s);" content
        | Int(signed, width) ->
            write level "ni.push%sInt%d(%s);" (if signed then "" else "U") (widthToInt width) content
        | Float32 -> write level "ni.pushFloat(%s);" content
        | Float64 -> write level "ni.pushDouble(%s);" content
        | StringType -> write level "ni.pushString(%s);" content
    | Resolved (resolvedName, _) ->
        let name =
            // all other types:
            resolvedNameToString context resolvedName (WrapperMethodMode "__%s") // see note above in this same branch of popValue
        if containsRefs then
            write level "%s__push(%s, %s);" name content (isReturnString())
        else
            write level "%s__push(%s);" name content
    | ArrayType (Primitive prim) ->
        match prim with
        | SizeT -> write level "ni.pushSizeTArray(%s);" content
        | Bool -> write level "ni.pushBoolArray(%s);" content
        | Int(signed, width) ->
            write level "ni.push%sInt%dArray(%s);" (if signed then "" else "U") (widthToInt width) content
        | Float32 -> write level "ni.pushFloatArray(%s);" content
        | Float64 -> write level "ni.pushDoubleArray(%s);" content
        | StringType -> write level "ni.pushStringArray(%s);" content
    | _ ->
        if containsRefs then
            write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
        else
            write level "__%s__push(%s);" (typeToName context type_) content

type ArrayOrSetMode =
    | ArrayMode
    | SetMode
type ArrayOrSetRenderer(context: DlangRenderContext, writer: IFormatWriter, outerType: NIType, elemType: NIType, mode: ArrayOrSetMode) =
    let write, blank = writer.Write, writer.Blank
    let pushSuffix =
        if typeContainsRefs context.ModuleContext (NITypeRoot outerType) then
            ", bool isReturn"
        else
            ""
    let renderDefault () =
        // use this for any types with no particular specializations (typedefs, delegates, arrays of arrays -- everything but structs and tuples, basically)
        match mode with
        | ArrayMode ->
            // push
            write 0 "private void __%s__push(%s values%s) {" (typeToName context outerType) (typeToDlangString context outerType) pushSuffix
            write 1 "import std.range: retro;"
            write 1 "foreach (value; retro(values)) {"
            pushValue context writer 2 elemType "value" (Indirect "isReturn")
            write 1 "}"
            pushValue context writer 1 (Primitive SizeT) "values.length" NotApplicable
            write 0 "}"
            // pop
            blank()
            write 0 "private %s __%s__pop() {" (typeToDlangString context outerType) (typeToName context outerType)
            popValue context writer 1 (Primitive SizeT) (NewVariable "count")
            write 1 "auto ret = new %s[count];" (typeToDlangString context elemType)
            write 1 "for (auto i = 0; i < count; i++) {"
            popValue context writer 2 elemType (ExistingVar "ret[i]")
            write 1 "}"
            write 1 "return ret;"
            write 0 "}"
        | SetMode ->
            write 0 "// renderDefault() SetMode for %s not yet implemented" (typeToDlangString context outerType)
    interface IArrayLikeRenderer with
        member this.TypedefTuple (tt: NIType) =
            let seqType, paramName =
                match mode with
                | ArrayMode -> ArrayType tt, "items"
                | SetMode -> SetType tt, "set"
            // push
            write 0 "pragma(inline)"
            write 0 "private void __%s__push(%s %s%s) {" (typeToName context outerType) (typeToDlangString context outerType) paramName pushSuffix
            pushValue context writer 1 seqType paramName (Indirect "isReturn")
            write 0 "}"
            // pop
            blank()
            write 0 "pragma(inline)"
            write 0 "private %s __%s__pop() {" (typeToDlangString context outerType) (typeToName context outerType)
            popValue context writer 1 seqType Return
            write 0 "}"
        member this.TypedefPrim (prim: NIPrimitive) =
            match mode with
            | ArrayMode ->
                // push
                write 0 "pragma(inline)"
                write 0 "private void __%s__push(%s items) {" (typeToName context outerType) (typeToDlangString context outerType)
                pushValue context writer 1 (ArrayType (Primitive prim)) "items" NotApplicable
                write 0 "}"
                // pop
                blank()
                write 0 "pragma(inline)"
                write 0 "private %s __%s__pop() {" (typeToDlangString context outerType) (typeToName context outerType)
                popValue context writer 1 (ArrayType (Primitive prim)) Return
                write 0 "}"
            | SetMode ->
                failwith "ArrayOrSetRenderer: TypedefPrim (set mode) not yet implemented"
        member this.EnumType (info: EnumInfo) =
            let widthType =
                widthTypeForEnum context.ModuleContext info
            match mode with
            | ArrayMode ->
                // push
                write 0 "private void __%s__push(%s items) {" (typeToName context outerType) (typeToDlangString context outerType)
                write 1 "import std.algorithm.iteration: map;"
                write 1 "import std.range: array;"
                write 1 "auto intValues = items.map!(i => cast(%s) i).array();" (typeToDlangString context widthType)
                pushValue context writer 1 (ArrayType widthType) "intValues" NotApplicable
                write 0 "}"
                // pop
                blank()
                write 0 "%s __%s__pop() {" (typeToDlangString context outerType) (typeToName context outerType)
                write 1 "import std.algorithm.iteration: map;"
                write 1 "import std.range: array;"
                popValue context writer 1 (ArrayType widthType) (NewVariable "intValues")
                write 1 "auto enumRange = intValues.map!(i => cast(%s) i).array();" (typeToDlangString context elemType)
                write 1 "return enumRange.array();"
                write 0 "}"
            | SetMode ->
                // push
                write 0 "private void __%s__push(%s set) {" (typeToName context outerType) (typeToDlangString context outerType)
                write 1 "import std.algorithm.iteration: map;"
                write 1 "import std.range: array;"
                write 1 "auto intValues = set.array().map!(i => cast(%s) i).array();" (typeToDlangString context widthType)
                pushValue context writer 1 (ArrayType widthType) "intValues" NotApplicable
                write 0 "}"
                // pop
                blank()
                write 0 "private %s __%s__pop() {" (typeToDlangString context outerType) (typeToName context outerType)
                write 1 "import std.algorithm.iteration: map;"
                write 1 "import std.range: array;"
                popValue context writer 1 (ArrayType widthType) (NewVariable "intValues")
                write 1 "auto enumRange = intValues.map!(i => cast(%s) i);" (typeToDlangString context elemType)
                write 1 "return %s(enumRange);" (typeToDlangString context outerType)
                write 0 "}"
        member this.StructType (_: ResolvedName) (namedFields: NamedField list) =
            let fieldValuesJoined =
                namedFields
                |> List.indexed
                |> List.map (fun (i, _) -> sprintf "f%dvalues[i]" i)
                |> String.concat ", "
            match mode with
            | ArrayMode ->
                // push
                write 0 "private void __%s__push(%s items%s) {" (typeToName context outerType) (typeToDlangString context outerType) pushSuffix
                write 1 "auto count = items.length;"
                for i, field in namedFields |> List.indexed do
                    write 1 "auto f%dvalues = new %s[count];" i (typeToDlangString context field.Type_)
                write 1 "for (auto i = 0; i < count; i++) {"
                for i, field in namedFields |> List.indexed do
                    write 2 "f%dvalues[i] = items[i].%s;" i (lowerFirst field.Name)
                write 1 "}"
                // push arrays in reverse order
                for i, field in namedFields |> List.indexed |> List.rev do
                    pushValue context writer 1 (ArrayType field.Type_) (sprintf "f%dvalues" i) (Indirect "isReturn")
                write 0 "}"
                // pop
                blank()
                write 0 "private %s __%s__pop() {" (typeToDlangString context outerType) (typeToName context outerType)
                for i, field in namedFields |> List.indexed do
                    popValue context writer 1 (ArrayType field.Type_) (NewVariable (sprintf "f%dvalues" i))
                write 1 "auto count = f0values.length;"
                write 1 "auto ret = new %s[count];" (typeToDlangString context elemType)
                write 1 "for (auto i = 0; i < count; i++) {"
                write 2 "ret[i] = %s(%s);" (typeToDlangString context elemType) fieldValuesJoined
                write 1 "}"
                write 1 "return ret;"
                write 0 "}"
            | SetMode ->
                // push
                write 0 "private void __%s__push(%s set%s) {" (typeToName context outerType) (typeToDlangString context outerType) pushSuffix
                write 1 "auto values = set.array();"
                write 1 "auto count = values.length;"
                for i, field in namedFields |> List.indexed do
                    write 1 "auto f%dvalues = new %s[count];" i (typeToDlangString context field.Type_)
                write 1 "for (auto i = 0; i < count; i++) {"
                for i, field in namedFields |> List.indexed do
                    write 2 "f%dvalues[i] = values[i].%s;" i (lowerFirst field.Name)
                write 1 "}"
                // push in reverse order
                for i, field in namedFields |> List.indexed |> List.rev do
                    pushValue context writer 1 (ArrayType field.Type_) (sprintf "f%dvalues" i) (Indirect "isReturn")
                write 0 "}"
                // pop
                blank()
                write 0 "private %s __%s__pop() {" (typeToDlangString context outerType) (typeToName context outerType)
                write 1 "import std.range : iota;"
                write 1 "import std.algorithm : map;"
                for i, field in namedFields |> List.indexed do
                    popValue context writer 1 (ArrayType field.Type_) (NewVariable (sprintf "f%dvalues" i))
                write 1 "auto count = f0values.length;"
                write 1 "auto r = iota(count).map!(i => %s(%s));" (typeToDlangString context elemType) fieldValuesJoined
                write 1 "return %s(r);" (typeToDlangString context outerType)
                write 0 "}"
        member this.TupleType (tupleTypes: NIType list) =
            let fieldValuesJoined =
                tupleTypes
                |> List.indexed
                |> List.map (fun (i, _) -> sprintf "f%dvalues[i]" i)
                |> String.concat ", "
            match mode with
            | ArrayMode ->
                // push
                write 0 "private void __%s__push(%s items%s) {" (typeToName context outerType) (typeToDlangString context outerType) pushSuffix
                write 1 "auto count = items.length;"
                for i, fieldType in tupleTypes |> List.indexed do
                    write 1 "auto f%dvalues = new %s[count];" i (typeToDlangString context fieldType)
                write 1 "for (auto i = 0; i < count; i++) {"
                for i, _ in tupleTypes |> List.indexed do
                    write 2 "f%dvalues[i] = items[i][%d];" i i
                write 1 "}"
                // push in reverse
                for i, fieldType in tupleTypes |> List.indexed |> List.rev do
                    pushValue context writer 1 (ArrayType fieldType) (sprintf "f%dvalues" i) (Indirect "isReturn")
                write 0 "}"
                // pop
                blank()
                write 0 "private %s __%s__pop() {" (typeToDlangString context outerType) (typeToName context outerType)
                for i, fieldType in tupleTypes |> List.indexed do
                    popValue context writer 1 (ArrayType fieldType) (NewVariable (sprintf "f%dvalues" i))
                write 1 "auto count = f0values.length;"
                write 1 "auto ret = new %s[count];" (typeToDlangString context elemType)
                write 1 "for (auto i = 0; i < count; i++) {"
                write 2 "ret[i] = tuple(%s);" fieldValuesJoined
                write 1 "}" // end for
                write 1 "return ret;"
                write 0 "}"
            | SetMode ->
                // push
                write 0 "private void __%s__push(%s set%s) {" (typeToName context outerType) (typeToDlangString context outerType) pushSuffix
                write 1 "auto items = set.array();"
                write 1 "auto count = items.length;"
                for i, fieldType in tupleTypes |> List.indexed do
                    write 1 "auto f%dvalues = new %s[count];" i (typeToDlangString context fieldType)
                write 1 "for (auto i = 0; i < count; i++) {"
                for i, _ in tupleTypes |> List.indexed do
                    write 2 "f%dvalues[i] = items[i][%d];" i i
                write 1 "}"
                // push in reverse:
                for i, fieldType in tupleTypes |> List.indexed |> List.rev do
                    pushValue context writer 1 (ArrayType fieldType) (sprintf "f%dvalues" i) (Indirect "isReturn")
                write 0 "}"
                // pop
                blank()
                write 0 "private %s __%s__pop() {" (typeToDlangString context outerType) (typeToName context outerType)
                write 1 "import std.range : iota;"
                write 1 "import std.algorithm : map;"
                for i, fieldType in tupleTypes |> List.indexed do
                    popValue context writer 1 (ArrayType fieldType) (NewVariable (sprintf "f%dvalues" i))
                write 1 "auto count = f0values.length;"
                write 1 "auto r = iota(count).map!(i => tuple(%s));" fieldValuesJoined
                write 1 "return %s(r);" (typeToDlangString context outerType)
                write 0 "}"
        member this.ArrayType () =
            renderDefault()
        member this.MapType () =
            renderDefault()
        member this.DelegateType () =
            renderDefault()
        member this.SetType () =
            renderDefault()
        member this.OptionType _elemType =
            failwith "ArrayOrSetRenderer: OptionType not yet implemented"
        member this.ResultType _succType _errType =
            failwith "ArrayOrSetRenderer: ResultType not yet implemented"
        member this.SumType () =
            renderDefault()
        member this.Primitive (prim: NIPrimitive) =
            assert (Primitive prim = elemType) // uhhh seems a bit redundant ...
            match mode with
            | ArrayMode ->
                // should be handled by built-in stack primitives
                // we're only printing something because otherwise a bunch of weird newlines at the top of the file :(
                write 0 "// built-in array type: %s" (typeToDlangString context outerType)
            | SetMode ->
                // push
                write 0 "private void __%s__push(%s items) {" (typeToName context outerType) (typeToDlangString context outerType)
                pushValue context writer 1 (ArrayType elemType) "items.array()" NotApplicable
                write 0 "}"
                // pop
                blank()
                write 0 "private %s __%s__pop() {" (typeToDlangString context outerType) (typeToName context outerType)
                let wrapFunc raw =
                    sprintf "%s(%s)" (typeToDlangString context outerType) raw
                popValue context writer 1 (ArrayType elemType) (WrapAndReturn wrapFunc)
                write 0 "}"
        member this.Options () =
            renderDefault()
        member this.Opaque () =
            match mode with
            | ArrayMode ->
                // push
                write 0 "private void __%s__push(%s items) {" (typeToName context outerType) (typeToDlangString context outerType)
                write 1 "import std.algorithm: map;"
                write 1 "import std.range: array;"
                write 1 "auto ptrs = items.map!(i => i.thisPtr).array();"
                write 1 "ni.pushPtrArray(ptrs);"
                write 0 "}"
                // pop
                blank()
                write 0 "private %s __%s__pop() {" (typeToDlangString context outerType) (typeToName context outerType)
                write 1 "import std.algorithm: map;"
                write 1 "import std.range: array;"
                write 1 "auto ptrs = ni.popPtrArray();"
                write 1 "return ptrs.map!(p => new %s(p)).array();" (typeToDlangString context elemType)
                write 0 "}"
            | SetMode ->
                // push
                write 0 "private void __%s__push(%s set) {" (typeToName context outerType) (typeToDlangString context outerType)
                write 1 "import std.algorithm: map;"
                write 1 "import std.range: array;"
                write 1 "auto ptrs = set.array().map!(i => i.thisPtr).array();"
                write 1 "ni.pushPtrArray(ptrs);"
                write 0 "}"
                // pop
                blank()
                write 0 "private %s __%s__pop() {" (typeToDlangString context outerType) (typeToName context outerType)
                write 1 "import std.algorithm: map;"
                write 1 "import std.range: array;"
                write 1 "auto ptrs = ni.popPtrArray();"
                write 1 "auto r = ptrs.map!(p => new %s(p));" (typeToDlangString context elemType)
                write 1 "return %s(r);" (typeToDlangString context outerType)
                write 0 "}"
        member this.Other (_: NIType) =
            let modeStr =
                if mode = ArrayMode then "array" else "set"
            write 2 "// TODO: ArrayOrSetRenderer .Other for %s of %s" modeStr (typeToDlangString context outerType)

let renderGeneric (context: DlangRenderContext) (writer: IFormatWriter) (gType: NIType) =
    let write, blank = writer.Write, writer.Blank
    //
    let pushSuffix =
        if typeContainsRefs context.ModuleContext (NITypeRoot gType) then
            ", bool isReturn"
        else
            ""
    blank()
    match gType with
    | ArrayType elemType ->
        let renderer =
            ArrayOrSetRenderer(context, writer, gType, elemType, ArrayMode)
        renderArrayLike context.ModuleContext elemType renderer
    | SetType elemType ->
        let renderer =
            ArrayOrSetRenderer(context, writer, gType, elemType, SetMode)
        renderArrayLike context.ModuleContext elemType renderer
    | MapType (keyType, valueType) ->
        // push
        write 0 "private void __%s__push(%s map%s) {" (typeToName context gType) (typeToDlangString context gType) pushSuffix
        pushValue context writer 1 (ArrayType valueType) "map.values" (Indirect "isReturn")
        pushValue context writer 1 (ArrayType keyType) "map.keys" (Indirect "isReturn")
        write 0 "}"
        // pop
        blank()
        write 0 "private %s __%s__pop() {" (typeToDlangString context gType) (typeToName context gType)
        popValue context writer 1 (ArrayType keyType) (NewVariable "keys")
        popValue context writer 1 (ArrayType valueType) (NewVariable "values")
        write 1 "assert(keys.length == values.length, \"key/value array lengths don't match\");"
        write 1 "%s result;" (typeToDlangString context gType)
        write 1 "for (size_t i = 0; i < keys.length; i++) {"
        write 2 "auto key = keys[i];"
        write 2 "result[key] = values[i];"
        write 1 "}"
        write 1 "return result;"
        write 0 "}"
    | TupleType tupleTypes ->
        // push
        write 0 "private void __%s__push(%s value%s) {" (typeToName context gType) (typeToDlangString context gType) pushSuffix
        for i, type_ in tupleTypes |> List.indexed |> List.rev do
            pushValue context writer 1 type_ (sprintf "value[%d]" i) (Indirect "isReturn")
        write 0 "}"
        // pop
        blank()
        write 0 "private %s __%s__pop() {" (typeToDlangString context gType) (typeToName context gType)
        for i, type_ in tupleTypes |> List.indexed do
            popValue context writer 1 type_ (NewVariable $"_%d{i}")
        let varsJoined =
            tupleTypes |> List.indexed |> List.map (fun (i, _) -> $"_%d{i}") |> String.concat ", "
        write 1 "return tuple(%s);" varsJoined
        write 0 "}"
    | BufferType elemType ->
        // push
        write 0 "private void __%s__push(%s buf, bool isReturn) {" (typeToName context gType) (typeToDlangString context gType)
        write 1 "ni.pushBuffer(buf, isReturn);"
        write 0 "}"
        // pop
        blank()
        write 0 "private %s __%s__pop() {" (typeToDlangString context gType) (typeToName context gType)
        write 1 "return ni.popBuffer!%s();" (typeToDlangString context elemType)
        write 0 "}"
    | OptionType elem ->
        // push
        write 0 "private void __%s__push(%s maybeValue%s) {" (typeToName context gType) (typeToDlangString context gType) pushSuffix
        write 1 "if (!maybeValue.isNull) {"
        pushValue context writer 2 elem "maybeValue.get" (Indirect "isReturn")
        pushValue context writer 2 (Primitive Bool) "true" NotApplicable
        write 1 "} else {"
        pushValue context writer 2 (Primitive Bool) "false" NotApplicable
        write 1 "}"
        write 0 "}"
        // pop
        blank()
        write 0 "private %s __%s__pop() {" (typeToDlangString context gType) (typeToName context gType)
        popValue context writer 1 (Primitive Bool) (NewVariable "hasValue")
        write 1 "if (hasValue) {"
        popValue context writer 2 elem (WrapAndReturn (sprintf "nullable(%s)"))
        write 1 "} // else:"
        write 1 "return %s();" (typeToDlangString context gType)
        write 0 "}"
    | ResultType (successType, errorType) ->
        // push
        write 0 "private void __%s__push(%s result%s) {" (typeToName context gType) (typeToDlangString context gType) pushSuffix
        write 1 "if (auto value = result.isSuccess()) {"
        pushValue context writer 2 successType "*value" (Indirect "isReturn")
        pushValue context writer 2 (Primitive Bool) "true" NotApplicable
        write 1 "} else {"
        pushValue context writer 2 errorType "result.getError()" (Indirect "isReturn")
        pushValue context writer 2 (Primitive Bool) "false" NotApplicable
        write 1 "}"
        write 0 "}"
        // pop
        blank()
        let wrapFunc ctor raw =
            sprintf "%s.%s(%s)" (typeToDlangString context gType) ctor raw
        write 0 "private %s __%s__pop() {" (typeToDlangString context gType) (typeToName context gType)
        popValue context writer 1 (Primitive Bool) (NewVariable "success")
        write 1 "if (success) {"
        popValue context writer 2 successType (WrapAndReturn (wrapFunc "Success"))
        write 1 "} // else:"
        popValue context writer 1 errorType (WrapAndReturn (wrapFunc "Error"))
        write 0 "}"
    | _ ->
        write 0 "// TODO: renderLibraryUserType: generic %s" (typeToDlangString context gType)

let joinedTypes (context: DlangRenderContext) (typesList: NIType list) =
    typesList
    |> List.map (typeToDlangString context)
    |> String.concat ", "


type ExtMethodStringMode =
    | MethodSignature
    | MethodHandle
    | ActualMethod

let extMethodToString (context: DlangRenderContext) (target: NIType) (method: NamedFunction) (mode: ExtMethodStringMode) =
    match mode with
    | MethodSignature ->
        // common func, match C++ signature:
        extMethodSignature target method
    | MethodHandle ->
        sprintf "_ext%s_%s" (typeToName context target) method.WrapperName
    | ActualMethod ->
        method.Name

let synthesizeExtensionMethods (_: DlangRenderContext) (info: ExtMethodsInfo) =
    info.Methods
    |> List.map (fun nf ->
        // inject 'this' arg
        let nextArgs =
            let firstArg =
                { Name = "_this"; Type = info.Target; DefaultValue = None }
            firstArg :: nf.Args
        { nf with Args = nextArgs })

let rec renderUserType (context: DlangRenderContext) (writer: IFormatWriter) (name: string) (userType: UserType) =
    let write, blank = writer.Write, writer.Blank

    let staticPrefix =
        if context.InStaticContext then
            "static "
        else
            ""

    let pushSuffix =
        if typeContainsRefs context.ModuleContext (UserTypeRoot userType) then
            ", bool isReturn"
        else
            ""

    blank()
    match userType with
    | OpaqueDef info ->
        let fakeThis =
            let namePath =
                context.GroupContext.Path @ [ name ]
            Resolved (ThisModule (SymbolName namePath), OpaqueType)

        // class setup is a bear with all this dispose/nodispose stuff :(
        let firstDisposable = isFirstDisposableOpaque context.ModuleContext info
        let isRoot = info.Parentage.Forced.MaybeParent.IsNone
        let inheritsOrImplementsSuffix =
            let finalList =
                let inheritsOrImplementsList =
                    info.Parentage.ResolvedNameList
                    |> List.map (fun name -> resolvedNameToString context name TypeMode)
                let disposableSuffix =
                    if firstDisposable then
                        [ "IDisposable" ]
                    else
                        []
                let interfaceSuffix =
                    if info.Parentage.ImplementsInterfaces then
                        [ "ni.InterfaceImplementer" ]
                    else
                        []
                inheritsOrImplementsList @ disposableSuffix @ interfaceSuffix
            if finalList.IsEmpty then
                ""
            else
                sprintf " : %s" (String.concat ", " finalList)

        write 0 "class %s%s {" (upperFirst name) inheritsOrImplementsSuffix
        // overload set stuff, sigh
        for overloadSet in info.MergeOverloadSets do
            write 1 "alias %s = %s.%s;" overloadSet.MethodName (typeToDlangString context overloadSet.Source) overloadSet.MethodName
        write 0 "package:"
        if isRoot then
            write 1 "ni.ObjectPtr thisPtr;"
        if firstDisposable then
            write 1 "bool disposed;"
        if isRoot then
            // normal ctor
            write 1 "this(ni.ObjectPtr obj) {"
            write 2 "this.thisPtr = obj;"
            write 1 "}"
            // public switch
            blank()
            write 0 "public:"
            // equals and hash
            write 1 "override bool opEquals(Object o) const {"
            write 2 "if (auto other = cast(%s)o) {" (upperFirst name)
            write 3 "return thisPtr == other.thisPtr;"
            write 2 "}"
            write 2 "return false;"
            write 1 "}"
            write 1 "override hash_t toHash() const {"
            write 2 "return typeid(thisPtr).getHash(&thisPtr);"
            write 1 "}"
        else
            // inherited
            write 1 "this(ni.ObjectPtr obj) {"
            write 2 "super(obj);"
            write 1 "}"
            // public switch
            blank()
            write 0 "public:"

        // dispose method
        if info.NoDispose then
            ()
        else
            write 1 "override void dispose() {"
            write 2 "if (!disposed) {"
            pushValue context writer 3 fakeThis "this" (Direct false)
            write 3 "ni.invokeModuleMethod(_%s_dispose);" (lowerFirst name)
            write 3 "disposed = true;"
            write 2 "}"
            write 1 "}"

        // nested function for convenience:
        let renderInstanceMethods (context: DlangRenderContext) =
            for namedFunc in info.Methods do
                blank()
                let throwsComment =
                    if namedFunc.Throws.IsEmpty then
                        ""
                    else
                        let joined =
                            namedFunc.Throws
                            |> List.map (typeToDlangString context)
                            |> String.concat ", "
                        sprintf " // throws %s" joined
                let shadowsOverrideStr =
                    // hmmm
                    if namedFunc.ShadowsExisting then
                        "/* shadows */ override "
                    else
                        ""
                write 1 "%s%s %s(%s)%s {" shadowsOverrideStr (typeToDlangString context namedFunc.Returns) (lowerFirst namedFunc.Name) (joinedNameFuncArgs context namedFunc.Args) throwsComment
                // push args in reverse
                for arg in namedFunc.Args |> List.rev do
                    pushValue context writer 2 arg.Type (safeIdentifier arg.Name) (Direct false)
                // push self argument
                pushValue context writer 2 fakeThis "this" (Direct false)
                // invoke remote
                if namedFunc.Throws.IsEmpty then
                    write 2 "ni.invokeModuleMethod(_%s_%s);" (lowerFirst name) namedFunc.WrapperName
                else
                    write 2 "ni.invokeModuleMethodWithExceptions(_%s_%s);" (lowerFirst name) namedFunc.WrapperName
                // return pop
                popValue context writer 2 namedFunc.Returns (if typeContainsRefs context.ModuleContext (NITypeRoot namedFunc.Returns) then SafeReturn else Return)
                write 1 "}"

        // static content
        match info.MaybeStaticDefs with
        | Some _ ->
            let nextContext =
                context.NavigateToSub(name)
            renderGroupCommon nextContext (writer.WithExtraIndent 1)
            // render methods in context of static block, so static types etc aren't unnecessarily prefixed
            renderInstanceMethods nextContext
        | None ->
            renderInstanceMethods context

        // interface implementations
        if info.Parentage.ImplementsInterfaces then
            blank()
            write 1 "// === interface implementations ==="
            write 1 "ni.ObjectPtr objectPtr() => thisPtr;"
            for iimpl in info.Parentage.Forced.Implements do
                match iimpl with
                | Resolved (name, _) ->
                    // render interface methods
                    write 1 "// %s:" (resolvedNameToString context name ResolvedNameStringMode.TypeMode)
                    writer.EatBlank()
                    match requiredSymbolLookup context.ModuleContext name with
                    | OpaqueInterfaceDef info ->
                        for namedFunc in info.Methods do
                            blank()
                            let throwsComment =
                                if namedFunc.Throws.IsEmpty then
                                    ""
                                else
                                    let joined =
                                        namedFunc.Throws
                                        |> List.map (typeToDlangString context)
                                        |> String.concat ", "
                                    sprintf " // throws %s" joined
                            let shadowsOverrideStr =
                                // hmmm
                                if namedFunc.ShadowsExisting then
                                    "/* shadows */ override "
                                else
                                    // always override since these are interface method implementations
                                    // 'shadows' stuff is more complicated for the moment ...
                                    "override "
                            write 1 "%s%s %s(%s)%s {" shadowsOverrideStr (typeToDlangString context namedFunc.Returns) (lowerFirst namedFunc.Name) (joinedNameFuncArgs context namedFunc.Args) throwsComment
                            // delegate to the free-standing top-level function
                            let funcName =
                                let methodPrefix =
                                    resolvedNameToString context name (ResolvedNameStringMode.WrapperMethodMode "_%s")
                                sprintf "%s_method_%s" methodPrefix (lowerFirst namedFunc.Name)
                            let allArgs =
                                { Name = "this"; Type = fakeThis; DefaultValue = None } :: namedFunc.Args
                            let invocation =
                                sprintf "%s(%s)" funcName (joinedNamedFuncArgNames allArgs)
                            match namedFunc.Returns with
                            | UnitType ->
                                write 2 "%s;" invocation
                            | _ ->
                                write 2 "return %s;" invocation
                            write 1 "}"
                    | _ ->
                        failwith "dlang renderUserType: unreachable rendering interface method"
                | _ ->
                    failwith "renderUserType: unreachable rendering opaque interface implementations block"

        // end class definition
        write 0 "}"

        // push =============
        blank()
        write 0 "pragma(inline)"
        write 0 "package %svoid __%s__push(%s thing) {" staticPrefix (upperFirst name) (upperFirst name)
        write 1 "if (thing !is null) {"
        write 2 "ni.pushObject(thing.thisPtr);"
        write 1 "} else {"
        write 2 "ni.pushNull();"
        write 1 "}"
        write 0 "}"

        // pop ==============
        blank()
        write 0 "pragma(inline)"
        write 0 "package %s%s __%s__pop() {" staticPrefix (upperFirst name) (upperFirst name)
        write 1 "auto obj = ni.popObject();"
        write 1 "if (obj.ptr !is null) {"
        write 2 "return new %s(obj);" (upperFirst name)
        write 1 "} else {"
        write 2 "return null;"
        write 1 "}"
        write 0 "}"

    | OpaqueInterfaceDef info ->
        let fakeThis =
            let namePath =
                context.GroupContext.Path @ [ name ]
            Resolved (ThisModule (SymbolName namePath), OpaqueInterfaceType)

        // actual interface definition
        write 0 "interface %s {" (upperFirst name)
        for method in info.Methods do
            let throwsComment =
                if method.Throws.IsEmpty then
                    ""
                else
                    let joined =
                        method.Throws
                        |> List.map (typeToDlangString context)
                        |> String.concat ", "
                    sprintf " // throws %s" joined
            write 1 "%s %s(%s);%s" (typeToDlangString context method.Returns) (lowerFirst method.Name) (joinedNameFuncArgs context method.Args) throwsComment
        write 0 "}" // end interface
        blank()

        // interface method bodies (sigh)
        // sadly it wasn't easy to use mixins because of the method handles, which I didn't want to expose
        // so we just create top-level functions that can be inlined elsewhere
        writer.EatBlank()
        for namedFunc in info.Methods do
            blank()
            write 0 "pragma(inline)"
            let throwsComment =
                if namedFunc.Throws.IsEmpty then
                    ""
                else
                    let joined =
                        namedFunc.Throws
                        |> List.map (typeToDlangString context)
                        |> String.concat ", "
                    sprintf " // throws %s" joined
            let funcName =
                sprintf "_%s_method_%s" (upperFirst name) (lowerFirst namedFunc.Name)
            let allArgs =
                { Name = "this_"; Type = fakeThis; DefaultValue = None } :: namedFunc.Args
            write 0 "package %s %s(%s) {%s" (typeToDlangString context namedFunc.Returns) funcName (joinedNameFuncArgs context allArgs) throwsComment
            // push args in reverse
            for arg in namedFunc.Args |> List.rev do
                pushValue context writer 1 arg.Type (safeIdentifier arg.Name) (Direct false)
            // push self argument
            pushValue context writer 1 fakeThis "this_" (Direct false)
            // invoke remote
            if namedFunc.Throws.IsEmpty then
                write 1 "ni.invokeModuleMethod(_%s_%s);" (lowerFirst name) namedFunc.WrapperName
            else
                write 1 "ni.invokeModuleMethodWithExceptions(_%s_%s);" (lowerFirst name) namedFunc.WrapperName
            // return pop
            popValue context writer 1 namedFunc.Returns (if typeContainsRefs context.ModuleContext (NITypeRoot namedFunc.Returns) then SafeReturn else Return)
            write 0 "}"

        blank()
        // default implementation (for popping raw interfaces)
        let firstDisposable = true // guaranteed for the moment (no inheritance, no @nodispose)
        let isRoot = true          // no inheritance yet
        write 0 "class %sImpl : %s, IDisposable, ni.InterfaceImplementer {" (upperFirst name) (upperFirst name)
        write 0 "package:"
        if isRoot then
            write 1 "ni.ObjectPtr thisPtr;"
        if firstDisposable then
            write 1 "bool disposed;"
        if isRoot then
            // normal ctor
            write 1 "this(ni.ObjectPtr obj) {"
            write 2 "this.thisPtr = obj;"
            write 1 "}"
            // public switch
            blank()
            write 0 "public:"
            // equals and hash
            write 1 "override bool opEquals(Object o) const {"
            write 2 "if (auto other = cast(ni.InterfaceImplementer)o) {"
            write 3 "return thisPtr == other.objectPtr();"
            write 2 "}"
            write 2 "return false;"
            write 1 "}"
            write 1 "override hash_t toHash() const {"
            write 2 "return typeid(thisPtr).getHash(&thisPtr);"
            write 1 "}"
        // dispose method
        write 1 "override void dispose() {"
        write 2 "if (!disposed) {"
        pushValue context writer 3 fakeThis "this" (Direct false)
        write 3 "ni.invokeModuleMethod(_%s_dispose);" (lowerFirst name)
        write 3 "disposed = true;"
        write 2 "}"
        write 1 "}" // end dispose()
        // interface implementation(s)
        blank()
        write 1 "ni.ObjectPtr objectPtr() => thisPtr;"
        for namedFunc in info.Methods do
            blank()
            let throwsComment =
                if namedFunc.Throws.IsEmpty then
                    ""
                else
                    let joined =
                        namedFunc.Throws
                        |> List.map (typeToDlangString context)
                        |> String.concat ", "
                    sprintf " // throws %s" joined
            let shadowsOverrideStr =
                // hmmm
                if namedFunc.ShadowsExisting then
                    "/* shadows */ override "
                else
                    // always override since these are interface method implementations
                    // 'shadows' stuff is more complicated for the moment ...
                    "override "
            write 1 "%s%s %s(%s)%s {" shadowsOverrideStr (typeToDlangString context namedFunc.Returns) (lowerFirst namedFunc.Name) (joinedNameFuncArgs context namedFunc.Args) throwsComment
            // delegate to the free-standing top-level function
            let funcName =
                sprintf "_%s_method_%s" (upperFirst name) (lowerFirst namedFunc.Name)
            let allArgs =
                { Name = "this"; Type = fakeThis; DefaultValue = None } :: namedFunc.Args
            let invocation =
                sprintf "%s(%s)" funcName (joinedNamedFuncArgNames allArgs)
            match namedFunc.Returns with
            | UnitType ->
                write 2 "%s;" invocation
            | _ ->
                write 2 "return %s;" invocation
            write 1 "}"
        write 0 "}" // end default implementation

        // push =============
        blank()
        write 0 "pragma(inline)"
        write 0 "package %svoid __%s__push(%s thing) {" staticPrefix (upperFirst name) (upperFirst name)
        write 1 "if (auto iimpl = cast(ni.InterfaceImplementer)thing) {"
        write 2 "ni.pushObject(iimpl.objectPtr());"
        write 1 "} else {"
        write 2 "ni.pushNull();"
        write 1 "}"
        write 0 "}"

        // pop ==============
        blank()
        write 0 "pragma(inline)"
        write 0 "package %s%s __%s__pop() {" staticPrefix (upperFirst name) (upperFirst name)
        write 1 "auto obj = ni.popObject();"
        write 1 "if (obj.ptr !is null) {"
        write 2 "return new %sImpl(obj);" (upperFirst name)
        write 1 "} else {"
        write 2 "return null;"
        write 1 "}"
        write 0 "}"

    | StructDef info ->
        let renderContent (context: DlangRenderContext) =
            for field in info.Fields do
                write 1 "%s %s;" (typeToDlangString context field.Type_) (lowerFirst field.Name)
            // default ctor
            write 1 "this(%s) {" (fieldsJoined context info.Fields)
            for field in info.Fields do
                write 2 "this.%s = %s;" (lowerFirst field.Name) (lowerFirst field.Name)
            write 1 "}"
        // actual struct
        write 0 "struct %s {" (upperFirst name)
        match info.MaybeStaticDefs with
        | Some _ ->
            let nextContext =
                context.NavigateToSub name
            renderContent nextContext
            write 1 "// static content ====="
            renderGroupCommon nextContext (writer.WithExtraIndent 1)
        | None ->
            renderContent context
        write 0 "}"
        // push
        blank()
        write 0 "package %svoid __%s__push(ref %s value%s) {" staticPrefix (upperFirst name) (upperFirst name) pushSuffix
        for field in info.Fields |> List.rev do
            let pushContent =
                sprintf "value.%s" (lowerFirst field.Name)
            pushValue context writer 1 field.Type_ pushContent (Indirect "isReturn")
        write 0 "}"
        // pop
        blank()
        write 0 "package %s%s __%s__pop() {" staticPrefix (upperFirst name) (upperFirst name)
        for field in info.Fields do
            popValue context writer 1 field.Type_ (NewVariable field.Name)
        write 1 "return %s(%s);" (upperFirst name) (fieldNamesJoined info.Fields)
        write 0 "}"

    | EnumDef info ->
        let widthType, widthSuffix =
            let type_ =
                widthTypeForEnum context.ModuleContext info
            let str =
                match type_ with
                | Primitive(Int(true, Width32)) -> ""
                | _ -> sprintf " : %s" (typeToDlangString context type_)
            type_, str
        // flags mode handling?
        write 0 "enum %s%s {" name widthSuffix
        if info.FlagsMode then
            write 1 "_none = 0," // otherwise D's default initialization will bite us in the ass!
        for (value, maybeSpecifiedValue), maybeComma in info.Values |> withTrailingCommas do
            match maybeSpecifiedValue with
            | Some specValue ->
                let enumValueStr =
                    constExprToString context None specValue
                write 1 "%s = %s%s" (upperFirst value) enumValueStr maybeComma
            | None ->
                write 1 "%s%s" (upperFirst value) maybeComma
        write 0 "}"
        // push
        blank()
        write 0 "pragma(inline)"
        write 0 "package %svoid __%s__push(%s value) {" staticPrefix (upperFirst name) (upperFirst name)
        let content =
            sprintf "cast(%s) value" (typeToDlangString context widthType)
        pushValue context writer 1 widthType content NotApplicable
        write 0 "}"
        // pop
        blank()
        write 0 "pragma(inline)"
        write 0 "package %s%s __%s__pop() {" staticPrefix (upperFirst name) (upperFirst name)
        let wrapFunc raw =
            sprintf "cast(%s) %s" (upperFirst name) raw
        popValue context writer 1 widthType (WrapAndReturn wrapFunc)
        write 0 "}"

    | NamedBitsDef info ->
        let indexedGroups, widthType =
            let resolver resolvedName =
                resolvedNameToString context resolvedName TypeMode
            namedBitsIndexer info context.ModuleContext resolver (upperFirst name)
        let widthSuffix =
            match widthType with
            | Primitive(Int(true, Width32)) -> ""
            | _ -> sprintf " : %s" (typeToDlangString context widthType)
        let maybeExtendsComment =
            match info.MaybeParent with
            | Some parent ->
                sprintf " // extends %s" (typeToDlangString context parent)
            | None ->
                ""
        write 0 "enum %s%s {%s // namedbits" (upperFirst name) widthSuffix maybeExtendsComment
        write 1 "_none = 0," // otherwise D's default initialization will bite us in the ass!
        for group, groupCommaStr in indexedGroups |> withTrailingCommas do
            match group.MaybeTitle with
            | Some title ->
                // title = bits inheritance origin
                write 1 "// %s" title
            | None ->
                ()
            for (index, bitName), commaStr in group.IndexedNames |> withTrailingCommas do
                let oneSuffix =
                    if index > 31 then "L" else ""
                write 1 "%s = 1%s << %d%s" bitName oneSuffix index (if commaStr = "" then groupCommaStr else ",")
        write 0 "}"
        // push
        blank()
        write 0 "package %svoid __%s__push(%s value) {" staticPrefix (upperFirst name) (upperFirst name)
        let pushContent =
            sprintf "cast(%s) value" (typeToDlangString context widthType)
        pushValue context writer 1 widthType pushContent NotApplicable
        write 0 "}"
        // pop
        blank()
        write 0 "package %s%s __%s__pop() {" staticPrefix (upperFirst name) (upperFirst name)
        let wrapFunc raw =
            sprintf "cast(%s) %s" (upperFirst name) raw
        popValue context writer 1 widthType (WrapAndReturn wrapFunc)
        write 0 "}"

    | OptionsDef namedFields ->
        if namedFields.Length > 31 then
            failwithf "DlangGenerator:renderUserType: options '%s' has more than 31 fields, the max currently supported" name
        let usedFieldsType =
            Primitive(Int(true, Width32))
        // definition
        write 0 "struct %s {" name
        write 0 "private:"
        write 1 "enum Fields {"
        for (i, field), maybeComma in namedFields |> List.indexed |> withTrailingCommas do
            write 2 "%s = %d%s" (upperFirst field.Name) (1 <<< i) maybeComma
        write 1 "}"
        write 1 "%s _usedFields;" (typeToDlangString context usedFieldsType)
        for field in namedFields do
            blank()
            write 1 "// %s" (upperFirst field.Name)
            let fieldTypeStr =
                typeToDlangString context field.Type_
            // storage
            write 1 "%s %s;" fieldTypeStr (lowerFirstWithUnderscore field.Name)
            // setter
            write 1 "@property"
            write 1 "public void %s(%s value) {" (lowerFirst field.Name) fieldTypeStr
            write 2 "%s = value;" (lowerFirstWithUnderscore field.Name)
            write 2 "_usedFields |= Fields.%s;" (upperFirst field.Name)
            write 1 "}"
            // 'hasField' getter
            write 1 "public bool has%s(out %s dest) {" (upperFirst field.Name) fieldTypeStr
            write 2 "if (_usedFields & Fields.%s) {" (upperFirst field.Name)
            write 3 "dest = %s;" (lowerFirstWithUnderscore field.Name)
            write 3 "return true;"
            write 2 "}"
            // write 2 "dest = %s.init;" fieldTypeStr; // skipped for now, not sure if good idea
            write 2 "return false;"
            write 1 "}"
            // getOrDefault (we'll probably remove this when/if we have a proper Option!T getter
            write 1 "public %s getOrDefault%s(%s defaultValue) {" fieldTypeStr (upperFirst field.Name) fieldTypeStr
            write 2 "if (_usedFields & Fields.%s) {" (upperFirst field.Name)
            write 3 "return %s;" (lowerFirstWithUnderscore field.Name)
            write 2 "}"
            write 2 "return defaultValue;"
            write 1 "}"
        // close struct
        write 0 "}"
        // push
        blank()
        write 0 "package %svoid __%s__push(ref %s value%s) {" staticPrefix (upperFirst name) (upperFirst name) pushSuffix
        for field in namedFields |> List.rev do
            write 1 "%s out%s;" (typeToDlangString context field.Type_) (upperFirst field.Name)
            write 1 "if (value.has%s(out%s)) {" (upperFirst field.Name) (upperFirst field.Name)
            pushValue context writer 2 field.Type_ $"out{upperFirst field.Name}" (Indirect "isReturn")
            write 1 "}"
        pushValue context writer 1 usedFieldsType "cast(int)value._usedFields" NotApplicable
        write 0 "}"
        // pop
        blank()
        write 0 "package %s%s __%s__pop() {" staticPrefix (upperFirst name) (upperFirst name)
        write 1 "%s opts;" (upperFirst name)
        popValue context writer 1 usedFieldsType (ExistingVar "opts._usedFields")
        for field in namedFields do
            write 1 "if (opts._usedFields & %s.Fields.%s) {" (upperFirst name) (upperFirst field.Name)
            popValue context writer 2 field.Type_ (ExistingVar $"opts.{lowerFirstWithUnderscore field.Name}")
            write 1 "}"
        write 1 "return opts;"
        write 0 "}"

    | SumTypeDef sumTypeValues ->
        // definition
        write 0 "struct %s {" (upperFirst name)
        let joinedValueNames =
            sumTypeValues
            |> List.map (fun value -> upperFirst value.Name)
            |> String.concat ", "
        write 1 "alias T = SumType!(%s);" joinedValueNames
        write 1 "T value;"
        write 1 "alias value this;"
        write 1 "private enum Tag {"
        for value, maybeComma in withTrailingCommas sumTypeValues do
            write 2 "%s%s" (upperFirst value.Name) maybeComma
        write 1 "}"
        // value definitions + factory method
        for value in sumTypeValues do
            blank()
            let joinedParams =
                joinedArgs context value.Args
            write 1 "// %s" (upperFirst value.Name)
            write 1 "class %s {" (upperFirst value.Name)
            if value.Args.IsEmpty then
                write 2 "// no params"
                write 2 "private this() {}"
            else
                for argName, argType in value.Args do
                    write 2 "%s %s;" (typeToDlangString context argType) argName
                write 2 "private this(%s) {" joinedParams
                for argName, _ in value.Args do
                    let lower = lowerFirst argName
                    write 3 "this.%s = %s;" lower lower
                write 2 "}"
            write 1 "}" // end class
            // factory method
            write 1 "static %s create%s(%s) {" (upperFirst name) (upperFirst value.Name) joinedParams
            write 2 "return %s(cast(%s.T) new %s(%s));" (upperFirst name) (upperFirst name) (upperFirst value.Name) (joinedArgNames value.Args)
            write 1 "}"
        write 0 "}" // end struct definition
        // push
        blank()
        write 0 "package %svoid __%s__push(%s value%s) {" staticPrefix (upperFirst name) (upperFirst name) pushSuffix
        write 1 "value.match!("
        for value, maybeComma in withTrailingCommas sumTypeValues do
            write 2 "(%s.%s actual) {" (upperFirst name) (upperFirst value.Name)
            if value.Args.IsEmpty then
                write 3 "// no params"
            else
                for argName, argType in value.Args |> List.rev do
                    let content =
                        sprintf "actual.%s" (lowerFirst argName)
                    pushValue context writer 3 argType content (Indirect "isReturn")
            // always write Tag of course
            write 3 "ni.pushInt32(%s.Tag.%s);" (upperFirst name) (upperFirst value.Name)
            write 2 "}%s" maybeComma
        write 1 ");" // end match
        write 0 "}" // end push
        // pop
        blank()
        write 0 "package %s%s __%s__pop() {" staticPrefix (upperFirst name) (upperFirst name)
        write 1 "auto tag = cast(%s.Tag) ni.popInt32();" (upperFirst name)
        write 1 "switch (tag) {"
        for value in sumTypeValues do
            write 2 "case %s.Tag.%s: {" (upperFirst name) (upperFirst value.Name)
            // pop params (if any) to variables of same name
            if value.Args.IsEmpty then
                write 3 "// no params"
            else
                for argName, argType in value.Args do
                    popValue context writer 3 argType (NewVariable (lowerFirst argName))
            write 3 "return %s.create%s(%s);" (upperFirst name) (upperFirst value.Name) (joinedArgNames value.Args)
            write 2 "}" // end case
        // default case (unreachable)
        write 2 "default:"
        write 3 "assert(0);"
        write 1 "}" // end switch
        write 0 "}" // end pop

    | TypeAliasDef niType ->
        write 0 "alias %s = %s;" (upperFirst name) (typeToDlangString context niType)
        blank()
        write 0 "pragma(inline)"
        write 0 "package void __%s__push(%s value%s) {" (upperFirst name) (upperFirst name) pushSuffix
        pushValue context writer 1 niType "value" (Indirect "isReturn")
        write 0 "}"
        blank()
        write 0 "pragma(inline)"
        write 0 "package %s __%s__pop() {" (upperFirst name) (upperFirst name)
        popValue context writer 1 niType Return
        write 0 "}"

    | InterfaceDef info ->
        // raw interface itself
        let parentInterface =
            match info.MaybeParent with
            | Some parent ->
                typeToDlangString context parent
            | None ->
                "ni.IClientInterface"
        write 0 "interface %s : %s {" (upperFirst name) parentInterface

        // nested function for convenience
        let renderMethods (context: DlangRenderContext) =
            for method in info.Methods do
                let throwsComment =
                    if method.Throws.IsEmpty then
                        ""
                    else
                        let joined =
                            method.Throws
                            |> List.map (typeToDlangString context)
                            |> String.concat ", "
                        sprintf " // throws %s" joined
                write 1 "%s %s(%s);%s" (typeToDlangString context method.Returns) (lowerFirst method.Name) (joinedNameFuncArgs context method.Args) throwsComment

        match info.MaybeStaticDefs with
        | Some _ ->
            let nextContext =
                context.NavigateToSub(name)
            renderMethods nextContext
            blank()
            write 0 "// static content =============="
            renderGroupCommon nextContext (writer.WithExtraIndent 1)
        | None ->
            renderMethods context
        // end of raw interface
        write 0 "}"
        // push
        blank()
        write 0 "package %svoid __%s__push(%s thing, bool isReturn) {" staticPrefix (upperFirst name) (upperFirst name)
        write 1 "ni.pushClientInterface(thing, isReturn);"
        write 0 "}"
        // pop
        blank()
        write 0 "package %s%s __%s__pop() {" staticPrefix (upperFirst name) (upperFirst name)
        write 1 "return ni.popClientInterface!%s();" (upperFirst name)
        write 0 "}"
    | ExceptionDef fields ->
        write 0 "class %s : Exception {" name
        if not fields.IsEmpty then
            write 0 "private:"
            for f in fields do
                write 1 "%s %s;" (typeToDlangString context f.Type_) (lowerFirstWithUnderscore f.Name)
        write 0 "public:"
        for f in fields do
            write 1 "%s %s() => %s;" (typeToDlangString context f.Type_) (lowerFirst f.Name) (lowerFirstWithUnderscore f.Name)
        // ctor
        if fields.IsEmpty then
            write 1 "this(string file = __FILE__, size_t line = __LINE__) {"
        else
            write 1 "this(%s, string file = __FILE__, size_t line = __LINE__) {" (fieldsJoined context fields)
            for f in fields do
                write 2 "this.%s = %s;" (lowerFirstWithUnderscore f.Name) (lowerFirst f.Name)
        write 2 "super(\"%s\", file, line);" name
        write 1 "}"
        write 0 "package:"
        // push + set
        write 1 "void pushAndSet() {"
        if fields.IsEmpty then
            write 2 "// no params"
        else
            for f in fields |> List.rev do
                pushValue context writer 2 f.Type_ (lowerFirstWithUnderscore f.Name) (Direct true) // true since pushed exceptions are basically returns, any reference fields need to be safely pushed
        write 2 "ni.setException(%s);" (lowerFirstWithUnderscore name)
        write 1 "}"
        // build and throw (pop)
        write 1 "static void buildAndThrow() {"
        if fields.IsEmpty then
            write 2 "// no params"
        else
            for f in fields do
                popValue context writer 2 f.Type_ (NewVariable f.Name)
        write 2 "throw new %s(%s);" name (fieldNamesJoined fields)
        write 1 "}"
        write 0 "}"

    | DelegateDef info ->
        let throwsList =
            match info.Throws with
            | [] -> ""
            | _ -> " // throws " + joinedTypes context info.Throws
        write 0 "alias %s = %s delegate(%s);%s" name (typeToDlangString context info.Returns) (joinedArgs context info.Args) throwsList
        // push
        blank()
        write 0 "package %svoid __%s__push(%s func) {" staticPrefix name name
        write 1 "void wrapper() {"
        for argName, argType in info.Args do
            popValue context writer 2 argType (NewVariable argName)
        let invocation =
            sprintf "func(%s)" (joinedArgNames info.Args)
        if info.Throws.IsEmpty then
            pushValue context writer 2 info.Returns invocation (Direct true)
        else
            write 2 "try {"
            pushValue context writer 3 info.Returns invocation (Direct true)
            for exc in info.Throws do
                write 2 "} catch (%s e) {" (typeToDlangString context exc)
                write 3 "e.pushAndSet();"
            write 2 "}"
        write 1 "}" // end wrapper
        write 1 "ni.pushClientFuncVal(&wrapper, func.ptr, func.funcptr);"
        write 0 "}"
        // pop
        blank()
        write 0 "package %s%s __%s__pop() {" staticPrefix name name
        write 1 "auto f = ni.popServerFuncVal();"
        write 1 "%s wrapper(%s) {" (typeToDlangString context info.Returns) (joinedArgs context info.Args)
        for argName, argType in info.Args |> List.rev do
            pushValue context writer 2 argType argName (Direct false)
        if info.Throws.IsEmpty then
            write 2 "f.exec();"
        else
            write 2 "f.execWithExceptions();"
        popValue context writer 2 info.Returns Return
        write 1 "}"
        write 1 "return &wrapper;"
        write 0 "}"

and renderGroupCommon (context: DlangRenderContext) (writer: IFormatWriter) =
    let write, blank = writer.Write, writer.Blank

    let groupContext =
        context.GroupContext

    let isRootGroup, contentPrefix =
        match groupContext.Kind with
        | ModuleRoot -> true, ""
        | _ -> false, "static "

    if isRootGroup then
        blank()
        write 0 "private:"
        blank()
    else
        write -1 "private:"

    // root module handle, if necessary
    // (would be nicer to do outside, BUT I like having it neatly below a single "private:"
    if isRootGroup then
        write 0 "__gshared ni.ModuleHandle _module;"

    // declare top level function handles
    for namedFunc in groupContext.ModuleMethods do
        write 0 "%s__gshared ni.ModuleMethodHandle _%s;" contentPrefix namedFunc.WrapperName

    // opaques
    for name, info in groupContext.Opaques do
        // instance methods
        for method in info.Methods do
            write 0 "%s__gshared ni.ModuleMethodHandle _%s_%s;" contentPrefix (lowerFirst name) method.WrapperName
        // synthesized dispose method
        if info.NoDispose then
            ()
        else
            write 0 "%s__gshared ni.ModuleMethodHandle _%s_dispose;" contentPrefix (lowerFirst name)

    // opaque interfaces
    for name, info in groupContext.OpaqueInterfaces do
        // instance methods
        for method in info.Methods do
            write 0 "%s__gshared ni.ModuleMethodHandle _%s_%s;" contentPrefix (lowerFirst name) method.WrapperName
        // synthesized dispose method
        // if info.NoDispose then
        //     ()
        // else
            write 0 "%s__gshared ni.ModuleMethodHandle _%s_dispose;" contentPrefix (lowerFirst name)

    // opaque extension methods
    for info in groupContext.ExtMethodBlocks do
        for method in info.Methods do
            let varName =
                extMethodToString context info.Target method MethodHandle
            write 0 "%s__gshared ni.ModuleMethodHandle %s;" contentPrefix varName

    // interfaces
    for name, info in groupContext.Interfaces do
        write 0 "%s__gshared ni.InterfaceHandle _%s;" contentPrefix (lowerFirst name)
        for method in info.Methods do
            write 0 "%s__gshared ni.InterfaceMethodHandle _%s_%s;" contentPrefix (lowerFirst name) method.WrapperName

    // exceptions
    for name, _ in groupContext.Exceptions do
        write 0 "%s__gshared ni.ExceptionHandle _%s;" contentPrefix (lowerFirst name)

    // globals declaration
    for name, type_ in groupContext.Globals do
        write 0 "%s__gshared %s _const_%s;" contentPrefix (typeToDlangString context type_) name

    blank()
    if isRootGroup then
        write 0 "public:"
        blank()
    else
        write -1 "public:"

    writer.EatBlank()

    // public getters for globals ===========
    for name, type_ in groupContext.Globals do
        blank()
        write 0 "%s%s %s() => _const_%s;" contentPrefix (typeToDlangString context type_) name name

    let renderNamedFunc (namedFunc: NamedFunction) (methodHandle: string) =
        blank()
        write 0 "%s%s %s(%s) {" contentPrefix (typeToDlangString context namedFunc.Returns) (lowerFirst namedFunc.Name) (joinedNameFuncArgs context namedFunc.Args)
        // push args in reverse
        for arg in namedFunc.Args |> List.rev do
            pushValue context writer 1 arg.Type (safeIdentifier arg.Name) (Direct false)
        // invoke remote
        if namedFunc.Throws.IsEmpty then
            write 1 "ni.invokeModuleMethod(%s);" methodHandle
        else
            write 1 "ni.invokeModuleMethodWithExceptions(%s);" methodHandle
        // return pop
        popValue context writer 1 namedFunc.Returns (if typeContainsRefs context.ModuleContext (NITypeRoot namedFunc.Returns) then SafeReturn else Return)
        write 0 "}"

    // top-level functions ===========
    for namedFunc in groupContext.ModuleMethods do
        match isRootGroup, namedFunc.Name with
        | true, ("moduleInit" | "moduleShutdown") ->
            // ignore, we invoke it manually in the __init/__shutdown functions - no public wrapper wanted
            ()
        | _ ->
            renderNamedFunc namedFunc (sprintf "_%s" namedFunc.WrapperName)

    // extension method blocks (root level only) =============
    if isRootGroup then
        for info in groupContext.ExtMethodBlocks do
            blank()
            write 0 "// extension methods for [%s] =============" (typeToDlangString context info.Target)
            writer.EatBlank()
            for method in synthesizeExtensionMethods context info do
                let handle =
                    extMethodToString context info.Target method MethodHandle
                renderNamedFunc method handle
            write 0 "// ========================================"

    // render user types + nested groups in order of declaration
    for def in groupContext.Defs do
        match def with
        | UserType(name, userType) ->
            renderUserType context writer name userType
        | Group(name, _) ->
            let group =
                groupContext.SubGroups[name]
            let nextContext =
                let nextSubRenderContext =
                    { context.RenderContext with GroupContext = group }
                { context with RenderContext = nextSubRenderContext } // why doesn't nested update work for us??
            blank()
            write 0 "// named group: %s" group.Name
            write 0 "struct %s {" group.Name
            renderGroupCommon nextContext (writer.WithExtraIndent 1)
            write 0 "}"
        | _ ->
            ()

let assignModuleHandles (context: DlangRenderContext) (writer: IFormatWriter) =
    let write, blank = writer.Write, writer.Blank

    let rec assignGroup (group: GroupContext) =
        let mkSourceHandle name =
            joinNameParts "." (group.Path @ [ name ])
        let mkTargetHandle name =
            joinNameParts "." (group.Path @ [ "_" + lowerFirst name ])

        // module method handles
        if not group.ModuleMethods.IsEmpty then
            for namedFunc in group.ModuleMethods do
                let source = mkSourceHandle namedFunc.WrapperName
                let target = mkTargetHandle namedFunc.WrapperName
                write 1 "%s = ni.getModuleMethod(_module, \"%s\");" target source

        // opaque method handles
        if not group.Opaques.IsEmpty then
            for name, info in group.Opaques do
                let sourceBase =
                    mkSourceHandle name
                let targetBase =
                    mkTargetHandle name
                // instance methods
                for method in info.Methods do
                    write 1 "%s_%s = ni.getModuleMethod(_module, \"%s_%s\");" targetBase method.WrapperName sourceBase method.WrapperName
                // synthesized dispose method handle
                if info.NoDispose then
                    ()
                else
                    write 1 "%s_dispose = ni.getModuleMethod(_module, \"%s_dispose\");" targetBase sourceBase

        // opaqueinterface method handles
        if not group.OpaqueInterfaces.IsEmpty then
            for name, info in group.OpaqueInterfaces do
                let sourceBase =
                    mkSourceHandle name
                let targetBase =
                    mkTargetHandle name
                // instance methods
                for method in info.Methods do
                    write 1 "%s_%s = ni.getModuleMethod(_module, \"%s_%s\");" targetBase method.WrapperName sourceBase method.WrapperName
                // // synthesized dispose method handle
                // if info.NoDispose then
                //     ()
                // else
                    write 1 "%s_dispose = ni.getModuleMethod(_module, \"%s_dispose\");" targetBase sourceBase

        // extension method handles
        if not group.ExtMethodBlocks.IsEmpty then
            for info in group.ExtMethodBlocks do
                for method in info.Methods do
                    let target =
                        extMethodToString context info.Target method MethodHandle
                    let signature =
                        extMethodToString context info.Target method MethodSignature
                    write 1 "%s = ni.getModuleMethod(_module, \"%s\");" target signature

        // exceptions
        if not group.Exceptions.IsEmpty then
            for name, _ in group.Exceptions do
                blank()
                let source =
                    mkSourceHandle name
                let target =
                    mkTargetHandle name
                write 1 "%s = ni.getException(_module, \"%s\");" target source
                write 1 "ni.setExceptionBuilder(%s, &%s.buildAndThrow);" target source

        // interface method handles, wrappers
        if not group.Interfaces.IsEmpty then
            for ifaceName, info in group.Interfaces do
                let sourceIface =
                    mkSourceHandle ifaceName
                let targetIface =
                    mkTargetHandle ifaceName
                write 1 "%s = ni.getInterface(_module, \"%s\");" targetIface sourceIface
                // methods + wrappers
                if not info.Methods.IsEmpty then
                    for method in info.Methods do
                        blank()
                        // let sourceMethod =
                        //     sprintf "%s.%s" sourceIface method.Name
                        let targetMethodHandle =
                            mkTargetHandle (sprintf "%s_%s" ifaceName method.WrapperName)
                        write 1 "%s = ni.getInterfaceMethod(%s, \"%s\");" targetMethodHandle targetIface method.WrapperName
                        // wrapper ======
                        write 1 "ni.setClientMethodWrapper(%s_%s, (%s inst) {" targetIface method.WrapperName (upperFirst ifaceName)
                        // pop args
                        for arg in method.Args do
                            popValue context writer 2 arg.Type (NewVariable arg.Name)
                        // exec + return
                        let invocation =
                            sprintf "inst.%s(%s)" (lowerFirst method.Name) (joinedNamedFuncArgNames method.Args)
                        if method.Throws.IsEmpty then
                            pushValue context writer 2 method.Returns invocation (Direct true)
                        else
                            write 2 "try {"
                            pushValue context writer 3 method.Returns invocation (Direct true)
                            for exc in method.Throws do
                                match exc with
                                | Resolved (_, ExceptionType) ->
                                    write 2 "} catch (%s e) {" (typeToDlangString context exc)
                                    write 3 "e.pushAndSet();"
                                | _ ->
                                    failwith "Dlang interface wrappers: 'throws' contained non-exception type"
                            write 2 "}"
                        write 1 "});"

        for groupName in group.SubOrder do
            let nextGroup =
                group.SubGroups[groupName]
            blank()
            assignGroup nextGroup

    assignGroup context.GroupContext

let writeModule (moduleContext: ModuleContext) (writer: IFormatWriter) (libraryName: string) (maybeModulePrefix: string option) (simpleName: string) =
    let write, blank = writer.Write, writer.Blank
    let m =
        moduleContext.Module
    let renderContext =
        { LibraryName = libraryName
          // BaseNamespace = (baseNamespace.Split "." |> Array.toList)
          RenderContext = createRenderContext moduleContext }
    let root =
        renderContext.GroupContext

    let fullyQualifiedModulename =
        let prefix =
            maybeModulePrefix
            |> Option.map (fun prefix -> libraryName + "." + prefix)
            |> Option.defaultValue libraryName
        prefix + "." + simpleName
        |> _.ToLower()
    write 0 "module %s;" fullyQualifiedModulename

    // imports
    if not m.Imports.IsEmpty then
        blank()
        write 0 "// module imports"
        for importName in m.Imports do
            write 0 "import %s.%s;" (lower libraryName) (joinModuleName LowerOnly importName) // baseNamespace not currently used in D

    blank()
    write 0 "import std.typecons: Tuple, tuple;"
    write 0 "import std.sumtype;"
    write 0 "import ni = %s.support.nativeimplclient;" (lower libraryName)
    write 0 "import std.typecons: Nullable, nullable;"
    write 0 "import stonesoup.disposable: IDisposable;"
    write 0 "import stonesoup.simpleset: Set;"
    write 0 "import stonesoup.result: Result;"

    // render all the generics at the root level only
    for generic in allGenericTypes moduleContext do
        renderGeneric renderContext writer generic

    // now with the common group stuff:
    renderGroupCommon renderContext writer

    // static init + shutdown ==========================================
    blank()
    write 0 "package void __init() {"
    write 1 "_module = ni.getModule(\"%s\");" (joinModuleName Preserve m.Name)

    // pop module constants, if any
    if countGroupConstants root > 0 then
        blank()
        write 1 "// assign module constants"
        write 1 "ni.pushModuleConstants(_module);"
        // recursively pop and assign module constants
        let rec popConstants (group: GroupContext) =
            for name, type_ in group.Globals do
                let target =
                    joinNameParts "." (group.Path @ [ "_const_" + name ])
                popValue renderContext writer 1 type_ (ExistingVar target)
            for subName in group.SubOrder do
                let sub = group.SubGroups[subName]
                popConstants sub
        popConstants root

    // assign all the other method handles etc
    // no sense creating a bunch of nested .Init() methods since we have all that information now!
    blank()
    write 1 "// assign module handles"
    assignModuleHandles renderContext writer

    // invoke user's ModuleInit, if it exists
    // ideally we wouldn't have to check its signature, would be nice if a validator caught that
    let maybeModuleInit =
        root.ModuleMethods
        |> List.tryFind (fun nf -> nf.Name = "moduleInit" && nf.Returns = UnitType && nf.Args = [])
    blank()
    match maybeModuleInit with
    | Some _ ->
        write 1 "ni.invokeModuleMethod(_moduleInit);"
    | None ->
        write 1 "// no static init [signature must be: void moduleInit()]"
    write 0 "}" // end of __init()

    // shutdown
    blank()
    write 0 "package void __shutdown() {"
    let maybeModuleShutdown =
        root.ModuleMethods
        |> List.tryFind (fun nf -> nf.Name = "moduleShutdown" && nf.Returns = UnitType && nf.Args = [])
    match maybeModuleShutdown with
    | Some _ ->
        write 1 "ni.invokeModuleMethod(_moduleShutdown);"
    | None ->
        write 1 "// no static shutdown [signature must be: void moduleShutdown()]"
    write 0 "}" // end of __shutdown()

let projectDir (outParams: OutParams) =
    sprintf "%s/client/dlang/%s" outParams.Path (lower outParams.LibraryName)

let generateModule (context: ModuleContext) (outParams: OutParams) =
    let baseNamespace, maybeModulePrefix, simpleName =
        namespaceParts outParams context.Module.Name
    let moduleDir =
        let subPath =
            maybeModulePrefix
            |> Option.map (fun mp -> "/" + mp.Replace(".", "/"))
            |> Option.defaultValue ""
        sprintf "%s/source/%s%s" (projectDir outParams) (lower outParams.LibraryName) subPath
    Directory.CreateDirectory(moduleDir) |> ignore
    let filename =
        sprintf "%s/%s.d" moduleDir (simpleName.ToLower())
    let modulePrefixString =
        maybeModulePrefix
        |> Option.map (fun mp -> $"{mp}.")
        |> Option.defaultValue ""
    printfn "Dlang writing (%s.%s%s) [%s]" baseNamespace modulePrefixString simpleName filename
    use output =
        let writer = new StreamWriter(filename)
        new SimpleFormatWriter(writer, 0)
    writeModule context output outParams.LibraryName maybeModulePrefix simpleName

let writeLibrary (allModules: Module list) (writer: IFormatWriter) (libraryName: string) =
    let write, blank = writer.Write, writer.Blank

    write 0 "module %s.library;" libraryName
    blank()
    write 0 "import ni = %s.support.nativeimplclient;" libraryName
    for m in allModules do
        let simple =
            joinModuleName LowerOnly m.Name
        write 0 "import %s.%s;" libraryName simple
    blank()
    write 0 "void libraryinit() {"
    write 1 "ni.init();"
    blank()
    // register modules in dependency order (`allModules` is provided that way already)
    for m in allModules do
        write 1 "%s.%s.__init();" libraryName (joinModuleName LowerOnly m.Name)
    write 0 "}"
    blank()
    write 0 "void libraryshutdown() {"
    // shutdown in reverse order
    for m in allModules |> List.rev do
        write 1 "%s.%s.__shutdown();" libraryName (joinModuleName LowerOnly m.Name)
    blank()
    // NI shutdown
    write 1 "ni.shutdown();"
    write 0 "}"

let generateLibraryFile (allModules: Module list) (outParams: OutParams) =
    let baseNamespace, maybeModulePrefix, simpleName =
        namespaceParts outParams (ModuleName ["Library"])
    let moduleDir =
        let subPath =
            maybeModulePrefix
            |> Option.map (fun mp -> "/" + mp.Replace(".", "/"))
            |> Option.defaultValue ""
        sprintf "%s/source/%s%s" (projectDir outParams) (lower outParams.LibraryName) subPath
    Directory.CreateDirectory(moduleDir) |> ignore
    let filename =
        sprintf "%s/%s.d" moduleDir (simpleName.ToLower())
    let modulePrefixString =
        maybeModulePrefix
        |> Option.map (fun mp -> $"{mp}.")
        |> Option.defaultValue ""
    printfn "Dlang writing (%s.%s%s) [%s]" baseNamespace modulePrefixString simpleName filename
    use output =
        let writer = new StreamWriter(filename)
        new SimpleFormatWriter(writer, 0)
    writeLibrary allModules output (lower outParams.LibraryName)

let writeProject (writer: IFormatWriter) (outParams: OutParams) =
    let write, blank = writer.Write, writer.Blank
    write 0 "name \"%s\"" (lower outParams.LibraryName)
    write 0 "description \"(description will go here)\""
    write 0 "authors \"nobody\""
    write 0 "copyright \"Copyright ©2026, Nobody\""
    write 0 "license \"TODO\""
    blank()
    write 0 "targetType \"library\""
    write 0 "targetPath \"./lib\""
    blank()
    // we're going to have to query the backend generator somehow to ask it where things are (the .DLL and .LIB specifically)
    // on that note, we also need to fold DynlibGenerator.fs into CppGenerator, since those are no longer separate things
    write 0 "// hardcoded for the moment, need to locate the server .lib file in an intelligent way"
    write 0 "libs \"$PACKAGE_DIR/../../../server/cpp/_dllproject/cmake-build-debug/%sServer\"" outParams.LibraryName
    blank()
    write 0 "dependency \"stonesoup\" path=\"R:/projects/dlang/stonesoup\""

let generateProject (outParams: OutParams) =
    let filename =
        sprintf "%s/dub.sdl" (projectDir outParams)
    use output =
        let writer = new StreamWriter(filename)
        new SimpleFormatWriter(writer, 0)
    writeProject output outParams

let copySupportFiles (outParams: OutParams) =
    let libraryName =
        lower outParams.LibraryName
    let destRoot =
        sprintf "%s/source/%s/support" (projectDir outParams) libraryName
    let copyFunc =
        let replaceMap =
            let importPrefix =
                sprintf "%s_ni_" libraryName
            [ ("%%LIBRARY_PREFIX%%", libraryName)
              ("%%DLL_IMPORT_PREFIX%%", importPrefix) ]
            |> Map.ofList
        SupportCopier.replaceCopyFunc replaceMap
    SupportCopier.copyFiles "client/dlang" destRoot true ["*.d"] true (Some copyFunc)
