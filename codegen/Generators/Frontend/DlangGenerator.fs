module Generators.Frontend.DlangGenerator

open System.IO
open AppCommon
open Generators.Common
open Parser
open Util

type DlangRenderContext = {
    BaseNamespace: string list
    RenderContext: RenderContext
    OpaqueMethodContext: OpaqueInfo option         // rendering an opaque method (pop/invocation/push) at the current group/path, prone to extra collisions on the push/pop statements
} with
    member this.BaseNamespaceJoined =
        joinNameParts "." this.BaseNamespace
    member this.ModuleContext =
        this.RenderContext.ModuleContext
    member this.GroupContext =
        this.RenderContext.GroupContext
    member this.NavigateToSub (name: string) =
        let nextRenderContext =
            this.RenderContext.NavigateToSub name
        { this with RenderContext = nextRenderContext }

let resolvedNameToString (context: DlangRenderContext) (resolvedName: ResolvedName) (mode: ResolveNameStringMode) =
    let qualification =
        let prefix =
            if mode = GenericIdentifierMode then
                // colliding with local symbols not a concern in this context, so we can use any (or probably even no) prefix
                ["G"]
            else
                context.BaseNamespace
        Forced prefix
    resolvedNameToStringAbstract "." (fun name _ -> name) None qualification context.RenderContext resolvedName mode

let constExprToString (context: DlangRenderContext) =
    let config =
        { NullStr = "null"
          ScopeResStr = "." }
    let strFunc resolvedName =
        resolvedNameToString context resolvedName TypeMode
    typedConstExprString config strFunc

let typeToDlangString (context: DlangRenderContext) (outerType: NIType) =
    let rec recInner = function
        | UnitType -> "void"
        | Primitive prim ->
            match prim with
            | SizeT -> "size_t"
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
            | FwdOpaqueType ->
                match requiredSymbolLookup context.ModuleContext resolvedName with
                | FwdOpaqueDef parts ->
                    sprintf "%s" (joinNameParts "." (context.BaseNamespace @ parts))
                | _ ->
                    failwith "typeToDlangString: failed to find fwdopaque definition"
            | TypedefType ->
                failwith "Dlang typeToDlangString : Resolved(...) : TypedefType - needs inspection"
                // // no C# typedefs so we just render the type directly
                // match requiredSymbolLookup context.ModuleContext resolvedName with
                // | TypeDef actual ->
                //     recInner actual
                // | _ ->
                //     failwithf "typeToDlangString: failed to find typedef definition"
            | _ ->
                resolvedNameToString context resolvedName TypeMode
        | ArrayType elemType -> sprintf "%s[]" (recInner elemType)
        // | SetType elemType -> sprintf "HashSet<%s>" (recInner elemType)
        // | BufferType elemType -> sprintf "INativeBuffer<%s>" (recInner elemType)
        | TupleType tupleTypes ->
            let joined =
                tupleTypes
                |> List.map recInner
                |> String.concat ", "
            sprintf "Tuple!(%s)" joined
        | MapType (keyType, valueType) ->
            sprintf "%s[%s]" (recInner valueType) (recInner keyType)
        // | OptionType elemType ->
        //     sprintf "Maybe<%s>" (recInner elemType)
        // | ResultType (succType, errType) ->
        //     sprintf "Result<%s,%s>" (recInner succType) (recInner errType)
        | x ->
            sprintf "(typeToCsString: not yet supported: %A)" x
    // go
    recInner outerType

let rec newDlangArray (context: DlangRenderContext) (ofType: NIType) (countVar: string) =
    match ofType with
    | ArrayType niType ->
        sprintf "%s[]" (newDlangArray context niType countVar)
    | _ ->
        sprintf "new %s[%s]" (typeToDlangString context ofType) countVar

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
        | Resolved (resolvedName, resolvedType) ->
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
    [for name, type_ in args -> sprintf "%s %s" (typeToDlangString context type_) name]
    |> String.concat ", "

let joinedNameFuncArgs (context: DlangRenderContext) (args: NamedFunctionArg list) =
    [for arg in args ->
        match arg.DefaultValue with
        | Some value ->
            sprintf "%s %s = %s" (typeToDlangString context arg.Type) arg.Name (constExprToString context value)
        | None ->
            sprintf "%s %s" (typeToDlangString context arg.Type) arg.Name ]
    |> String.concat ", "

let joinedArgNames (args: (string * NIType) list) =
    [for name, _ in args -> name]
    |> String.concat ", "

let joinedNamedFuncArgNames (args: NamedFunctionArg list) =
    [for arg in args -> arg.Name]
    |> String.concat ", "

let fieldsJoined (context: DlangRenderContext) (fields: NamedField list) =
    [for f in fields -> sprintf "%s %s" (typeToDlangString context f.Type_) f.Name]
    |> String.concat ", "

let fieldNamesJoined (fields: NamedField list) =
    [for f in fields -> f.Name]
    |> String.concat ", "

type PopValueContext =
    | NewVariable of name: string
    | ExistingVar of name: string
    | Return
    | SafeReturn

let popValue (context: DlangRenderContext) (writer: IFormatWriter) (level: int) (type_: NIType) (popContext: PopValueContext) =
    let write = writer.Write
    let contextPrefix =
        match popContext with
        | NewVariable name -> sprintf "auto %s =" name
        | ExistingVar name -> sprintf "%s =" name
        | Return -> "return"
        | SafeReturn -> "auto __ret ="
    match type_ with
    | UnitType ->
        match popContext with
        | Return -> () // nothing'
        | _ -> failwith "tried to pop a Unit value in a non-Return context, why?"
    | Primitive prim ->
        match prim with
        | SizeT -> write level "%s ni.popSizeT();" contextPrefix
        | Bool -> write level "%s ni.popBool();" contextPrefix
        | Int(signed, width) ->
            write level "%s ni.pop%sInt%d();" contextPrefix (if signed then "" else "U") (widthToInt width)
        | Float32 -> write level "%s ni.popFloat();" contextPrefix
        | Float64 -> write level "%s ni.popDouble();" contextPrefix
        | StringType -> write level "%s ni.popString();" contextPrefix
    | Resolved (resolvedName, resolvedType) ->
        let name =
            match resolvedType with
            | FwdOpaqueType ->
                // special treatment for these:
                match requiredSymbolLookup context.ModuleContext resolvedName with
                | FwdOpaqueDef parts ->
                    sprintf "%s" (joinNameParts "." (context.BaseNamespace @ parts))
                | _ ->
                    failwithf "Dlang popValue: FwdOpaque trouble, should never happen"
            | _ ->
                // all other types:
                resolvedNameToString context resolvedName TypeMode
        write level "%s __%s__pop();" contextPrefix name
    | ArrayType niType ->
        match niType with
        | Primitive prim ->
            match prim with
            | Bool -> write level "%s ni.popBoolArray();" contextPrefix
            | Int(signed, width) ->
                write level "%s ni.pop%sInt%dArray();" contextPrefix (if signed then "" else "U") (widthToInt width)
            | Float32 -> write level "%s ni.popFloatArray();" contextPrefix
            | Float64 -> write level "%s ni.popDoubleArray();" contextPrefix
            | StringType -> write level "%s ni.popStringArray();" contextPrefix
            | _ -> write level "// popValue not yet supported: array of prim %A" prim
        | Resolved _ ->
            write level "%s __%s__pop();" contextPrefix (typeToName context (ArrayType niType))
        | ArrayType _ -> write level "%s __%s__pop();" contextPrefix (typeToName context type_) // sigh, normalize all these already - doing it so many different ways...
        | SetType _ -> write level "%s __%s__pop();" contextPrefix (typeToName context type_)
        | MapType _ -> write level "%s __%s__pop();" contextPrefix (typeToName context type_)
        | TupleType _ -> write level "%s __%s__pop();" contextPrefix (typeToName context type_)
        | OptionType _ -> write level "%s __%s__pop();" contextPrefix (typeToName context type_)
        | ResultType _ -> write level "%s __%s__pop();" contextPrefix (typeToName context type_)
        | _ -> write level "// popValue not yet supported: array of %A" niType
    | MapType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName context type_)
    | TupleType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName context type_)
    | SetType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName context type_)
    | OptionType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName context type_)
    | ResultType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName context type_)
    | BufferType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName context type_)
    | _ ->
        write level "// popValue %A not yet supported" type_
    match popContext with
    | SafeReturn ->
        write level "ni.clearServerSafetyArea();"
        write level "return __ret;"
    | _ ->
        ()

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
    | Resolved(resolvedName, resolvedType) ->
        let name =
            match resolvedType with
            | FwdOpaqueType ->
                // special treatment for fwd opaques:
                match requiredSymbolLookup context.ModuleContext resolvedName with
                | FwdOpaqueDef parts ->
                    sprintf "%s" (joinNameParts "." (context.BaseNamespace @ parts))
                | _ ->
                    failwithf "Dlang pushValue: FwdOpaqueType trouble, shouldn't happen"
            | _ ->
                // all other types:
                resolvedNameToString context resolvedName TypeMode
        match resolvedType with
        | StructType | InterfaceType | TypedefType | SumType | OptionsType ->
            // types that potentially contain refs, need to honor isReturn
            write level "__%s__push(%s, %s);" name content (isReturnString())
        | _ ->
            write level "__%s__push(%s);" name content
    | ArrayType niType ->
        match niType with
        | Primitive prim ->
            match prim with
            | Bool -> write level "ni.pushBoolArray(%s);" content
            | Int(signed, width) ->
                write level "ni.push%sInt%dArray(%s);" (if signed then "" else "U") (widthToInt width) content
            | Float32 -> write level "ni.pushFloatArray(%s);" content
            | Float64 -> write level "ni.pushDoubleArray(%s);" content
            | StringType -> write level "ni.pushStringArray(%s);" content
            | _ -> write level "// pushValue not yet supported: Array of prim %A" prim
        | Resolved (_, resolvedType) ->
            match resolvedType with
            | StructType | InterfaceType | TypedefType | SumType | OptionsType ->
                write level "__%s__push(%s, %s);" (typeToName context (ArrayType niType)) content (isReturnString())
            | _ ->
                write level "__%s__push(%s);" (typeToName context (ArrayType niType)) content
        | ArrayType _ -> write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
        | SetType _ -> write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
        | MapType _ -> write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
        | TupleType _ -> write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
        | OptionType _ -> write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
        | ResultType _ -> write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
        | _ -> write level "// pushValue not yet supported: Array of %A" niType
    | TupleType _ ->
        write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
    | SetType niType ->
        match niType with
        | Primitive _ ->
            write level "__%s__push(%s);" (typeToName context type_) content
        | Resolved(_, resolvedType) ->
            match resolvedType with
            | StructType | InterfaceType | TypedefType | SumType | OptionsType ->
                write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
            | _ ->
                write level "__%s__push(%s);" (typeToName context type_) content
        | _ ->
            write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
    | BufferType _ ->
        write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
    | MapType _ ->
        write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
    | OptionType _ ->
        write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
    | ResultType _ ->
        write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
    | _ ->
        write level "// pushValue %A not yet supported" type_

// TODO: ArrayOrSetRenderer etc
// TODO: renderGeneric

let joinedTypes (context: DlangRenderContext) (typesList: NIType list) =
    typesList
    |> List.map (typeToDlangString context)
    |> String.concat ", "

let rec renderUserType (context: DlangRenderContext) (writer: IFormatWriter) (name: string) (userType: UserType) =
    let write, blank = writer.Write, writer.Blank

    // TODO: need to handle context-specific prefix
    // ie, 'static' on push/pop methods for user types, when in a static context
    // (named group, opaque static, etc)

    blank()
    match userType with
    | OpaqueDef info ->
        let fakeThis =
            let namePath =
                context.GroupContext.Path @ [ name ]
            Resolved (ThisModule (SymbolName namePath), OpaqueType)
        let maybeInheritsFrom =
            match info.MaybeParent with
            | Some (Resolved(resolvedName, OpaqueType)) ->
                Some resolvedName
            | _ ->
                None

        // class setup is a bear with all this dispose/nodispose stuff :(
        let firstDisposable = isFirstDisposableOpaque context.ModuleContext info
        let isRoot = maybeInheritsFrom.IsNone
        let inheritsOrImplementsSuffix =
            let finalList =
                let maybeParentOpaque =
                    maybeInheritsFrom
                    |> Option.map (fun parentName ->
                        resolvedNameToString context parentName TypeMode)
                let maybeDisposableString =
                    if firstDisposable then
                        Some "ni.IDisposable"
                    else
                        None
                [ maybeParentOpaque; maybeDisposableString ]
                |> List.choose id
            if finalList.IsEmpty then
                ""
            else
                sprintf " : %s" (String.concat ", " finalList)

        write 0 "class %s%s {" (upperFirst name) inheritsOrImplementsSuffix
        write 0 "protected:"
        if isRoot then
            write 1 "void *thisPtr;"
        if firstDisposable then
            write 1 "bool disposed;"
        if isRoot then
            // normal ctor
            write 1 "this(void *ptr) {"
            write 2 "this.thisPtr = ptr;"
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
            write 1 "this(void *ptr) {"
            write 2 "super(ptr);"
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
                let shadowsStr =
                    if namedFunc.ShadowsExisting then
                        "new "
                    else
                        ""
                write 1 "%s%s %s(%s)%s" shadowsStr (typeToDlangString context namedFunc.Returns) (lowerFirst namedFunc.Name) (joinedNameFuncArgs context namedFunc.Args) throwsComment
                write 1 "{"
                // another nested function here, because we're modifying the render context a bit
                let renderMethodInvocation (context: DlangRenderContext) =
                    // push args in reverse
                    for arg in namedFunc.Args |> List.rev do
                        pushValue context writer 2 arg.Type arg.Name (Direct false)
                    // push self argument
                    pushValue context writer 2 fakeThis "this" (Direct false)
                    // invoke remote
                    if namedFunc.Throws.IsEmpty then
                        write 2 "ni.invokeModuleMethod(_%s_%s);" (lowerFirst name) namedFunc.WrapperName
                    else
                        write 2 "ni.invokeModuleMethodWithExceptions(_%s_%s);" (lowerFirst name) namedFunc.WrapperName
                    // return pop
                    popValue context writer 2 namedFunc.Returns (if containsRefs context.ModuleContext namedFunc.Returns then SafeReturn else Return)
                // go
                renderMethodInvocation {context with OpaqueMethodContext = Some info }
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
        // end class definition
        write 0 "}"

        // push =============
        blank()
        write 0 "pragma(inline)"
        write 0 "package void __%s__push(%s thing) {" (upperFirst name) (upperFirst name)
        write 1 "ni.pushPtr(thing !is null ? thing.thisPtr : null);"
        write 0 "}"

        // pop ==============
        blank()
        write 0 "pragma(inline)"
        write 0 "package %s __%s__pop() {" (upperFirst name) (upperFirst name)
        write 1 "auto ptr = ni.popPtr();"
        write 1 "return ptr !is null ? new %s(ptr) : null;" (upperFirst name)
        write 0 "}"

    | EnumDef info ->
        let widthType, widthStr =
            match info.MaybeWidth with
            | Some widthType ->
                widthType, sprintf " : %s" (typeToDlangString context widthType)
            | None ->
                Primitive(Int(true,Width32)), ""
        // flags mode handling?
        write 0 "enum %s%s {" name widthStr
        for (value, maybeSpecifiedValue), maybeComma in info.Values |> withTrailingCommas do
            match maybeSpecifiedValue with
            | Some specValue ->
                let enumValueStr =
                    constExprToString context specValue
                write 1 "%s = %s%s" (upperFirst value) enumValueStr maybeComma
            | None ->
                write 1 "%s%s" (upperFirst value) maybeComma
        write 0 "}"
        // push
        blank()
        write 0 "pragma(inline)"
        write 0 "package void __%s__push(%s value) {" (upperFirst name) (upperFirst name)
        let content =
            sprintf "cast(%s) value" (typeToDlangString context widthType)
        pushValue context writer 1 widthType content NotApplicable
        write 0 "}"
        // pop
        blank()
        write 0 "pragma(inline)"
        write 0 "package %s __%s__pop() {" (upperFirst name) (upperFirst name)
        popValue context writer 1 widthType (NewVariable "ret")
        write 1 "return cast(%s) ret;" (upperFirst name)
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
        write 0 "package void __%s__push(ref %s value, bool isReturn) {" (upperFirst name) (upperFirst name)
        for field in namedFields |> List.rev do
            write 1 "%s out%s;" (typeToDlangString context field.Type_) (upperFirst field.Name)
            write 1 "if (value.has%s(out%s)) {" (upperFirst field.Name) (upperFirst field.Name)
            pushValue context writer 2 field.Type_ $"out{upperFirst field.Name}" (Indirect "isReturn")
            write 1 "}"
        pushValue context writer 1 usedFieldsType "cast(int)value._usedFields" NotApplicable
        write 0 "}"
        // pop
        blank()
        write 0 "package %s __%s__pop() {" (upperFirst name) (upperFirst name)
        write 1 "%s opts;" (upperFirst name)
        popValue context writer 1 usedFieldsType (ExistingVar "opts._usedFields")
        for field in namedFields do
            write 1 "if (opts._usedFields & %s.Fields.%s) {" (upperFirst name) (upperFirst field.Name)
            popValue context writer 2 field.Type_ (ExistingVar $"opts.{lowerFirstWithUnderscore field.Name}")
            write 1 "}"
        write 1 "return opts;"
        write 0 "}"

    | InterfaceDef info ->
        // raw interface itself
        let parentInterface =
            match info.MaybeParent with
            | Some parent ->
                typeToDlangString context parent
            | None ->
                "ni.IDisposable"
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
            failwith "Dlang InterfaceDef rendering: static block not yet supported"
            // let nextContext =
            //     context.NavigateToSub(name)
            // renderGroupCommon nextContext (writer.WithExtraIndent 1)
            // renderMethods nextContext
        | None ->
            renderMethods context
        // end of raw interface
        write 0 "}"
        // push
        blank()
        write 0 "package void __%s__push(%s thing, bool isReturn) {" (upperFirst name) (upperFirst name)
        write 1 "ni.pushInterface(thing, isReturn);"
        write 0 "}"
        // pop
        blank()
        write 0 "package %s __%s__pop() {" (upperFirst name) (upperFirst name)
        write 1 "bool isClientId;"
        write 1 "auto id = ni.popInstance(&isClientId);"
        write 1 "if (id != 0) {"
        write 2 "if (isClientId) {"
        write 3 "// if we're popping it, we must have pushed it in the past sometime"
        write 3 "return ni.getClientInterfaceAs!%s(id);" (upperFirst name)
        write 2 "}"
        write 2 "// always wrap server stuff in a fresh wrapper (no tables involved)"
        write 2 "return new Server%s(id);" (upperFirst name)
        write 1 "}"
        write 1 "return null;"
        write 0 "}"
        // server wrapper
        blank()
        let parentClass =
            match info.MaybeParent with
            | Some parent -> typeWithPrefix context parent "Server"
            | None -> "ni.ServerObject"
        write 0 "private class Server%s : %s, %s {" (upperFirst name) parentClass (upperFirst name)
        write 0 "public:"
        write 1 "this(int id) {"
        write 2 "super(id);"
        write 1 "}"
        // server wrapper methods
        for method in info.Methods do
            write 1 "%s %s(%s) {" (typeToDlangString context method.Returns) (lowerFirst method.Name) (joinedNameFuncArgs context method.Args)
            // push args in reverse
            for arg in method.Args |> List.rev do
                pushValue context writer 2 arg.Type arg.Name (Direct false)
            // invoke remote
            if method.Throws.IsEmpty then
                write 2 "ni.invokeInterfaceMethod(_%s_%s, id);" (lowerFirst name) method.WrapperName
            else
                write 2 "ni.invokeInterfaceMethodWithExceptions(_%s_%s, id);" (lowerFirst name) method.WrapperName
            // return (mindful of safety area nonsense)
            if containsRefs context.ModuleContext method.Returns then
                popValue context writer 2 method.Returns SafeReturn
            else
                popValue context writer 2 method.Returns Return
            write 1 "}"
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
        write 0 "package void __%s__push(%s func) {" name name
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
        write 0 "package %s __%s__pop() {" name name
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
    | _ ->
        write 0 "// unhandled user type %s" name

and renderGroupCommon (context: DlangRenderContext) (writer: IFormatWriter) =
    let write, blank = writer.Write, writer.Blank

    let groupContext =
        context.GroupContext

    let isRootGroup, contentPrefix =
        match groupContext.Kind with
        | RootGroup -> true, ""
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
        write 0 "ni.ModuleHandle _module;"

    // declare top level function handles
    for namedFunc in groupContext.ModuleMethods do
        write 0 "%sni.ModuleMethodHandle _%s;" contentPrefix namedFunc.WrapperName

    for name, info in groupContext.Opaques do
        // instance methods
        for method in info.Methods do
            write 0 "%sni.ModuleMethodHandle _%s_%s;" contentPrefix (lowerFirst name) method.WrapperName
        // synthesized dispose method
        if info.NoDispose then
            ()
        else
            write 0 "%sni.ModuleMethodHandle _%s_dispose;" contentPrefix (lowerFirst name)

    // interfaces
    for name, info in groupContext.Interfaces do
        write 0 "%sni.InterfaceHandle _%s;" contentPrefix (lowerFirst name)
        for method in info.Methods do
            write 0 "%sni.InterfaceMethodHandle _%s_%s;" contentPrefix (lowerFirst name) method.WrapperName

    // exceptions
    for name, _ in groupContext.Exceptions do
        write 0 "%sni.ExceptionHandle _%s;" contentPrefix (lowerFirst name)

    // globals declaration
    for name, type_ in groupContext.Globals do
        write 0 "%s%s %s;" contentPrefix (typeToDlangString context type_) (lowerFirstWithUnderscore name)

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
        write 0 "%s%s %s() => %s;" contentPrefix (typeToDlangString context type_) (lowerFirst name) (lowerFirstWithUnderscore name)

    // top-level functions ===========
    for namedFunc in groupContext.ModuleMethods do
        if namedFunc.Name = "moduleInit" || namedFunc.Name = "moduleShutdown" then
            // ignore, we invoke it manually in the __init/__shutdown functions - no public wrapper wanted
            ()
        else
            blank()
            write 0 "%s%s %s(%s) {" contentPrefix (typeToDlangString context namedFunc.Returns) (lowerFirst namedFunc.Name) (joinedNameFuncArgs context namedFunc.Args)
            // push args in reverse
            for arg in namedFunc.Args |> List.rev do
                pushValue context writer 1 arg.Type arg.Name (Direct false)
            // invoke remote
            if namedFunc.Throws.IsEmpty then
                write 1 "ni.invokeModuleMethod(_%s);" namedFunc.WrapperName
            else
                write 1 "ni.invokeModuleMethodWithExceptions(_%s);" namedFunc.WrapperName
            // return pop
            popValue context writer 1 namedFunc.Returns (if containsRefs context.ModuleContext namedFunc.Returns then SafeReturn else Return)
            write 0 "}"

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
            write 0 "static class %s {" group.Name
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

let writeModule (moduleContext: ModuleContext) (writer: IFormatWriter) (baseNamespace: string) (libraryName: string) (maybeModulePrefix: string option) (simpleName: string) =
    let write, blank = writer.Write, writer.Blank
    let m =
        moduleContext.Module
    let renderContext =
        { BaseNamespace = (baseNamespace.Split "." |> Array.toList)
          RenderContext = createRenderContext moduleContext
          OpaqueMethodContext = None }
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
    blank()
    //
    // TODO: other module imports!
    //
    write 0 "import std.typecons: Tuple, tuple;"
    write 0 "import ni = %s.support.nativeimplclient;" (lower libraryName)

    //
    // TODO: render generics just once, at root level
    //
    // now with the common group stuff:
    renderGroupCommon renderContext writer

    // static init + shutdown ==========================================
    blank()
    write 0 "package void __init() {"
    write 1 "_module = ni.getModule(\"%s\");" (joinModuleName m.Name)

    // pop module constants, if any
    if countGroupConstants root > 0 then
        blank()
        write 1 "// assign module constants"
        write 1 "ni.pushModuleConstants(_module);"
        // recursively pop and assign module constants
        let rec popConstants (group: GroupContext) =
            for name, type_ in group.Globals do
                let target =
                    joinNameParts "." (group.Path @ [ lowerFirstWithUnderscore name ])
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
    writeModule context output baseNamespace outParams.LibraryName maybeModulePrefix simpleName
