module Generators.Backend.CppGenerator

open System.IO
open AppCommon
open Parser

open Util
open Generators.Common
open Extensions

type ResolvedNameMode =
    | Normal
    | ImplementationHeader  // always use fully qualified imports, to prevent conflicts in places like Library.cpp
    | Wrappers
    | WrappersPushPop
    | ExtensionMethodsBlock // always use fully qualified, so the typeToName rendering matches everywhere

type CppRenderContext = {
    RenderContext: RenderContext
    MaybePathClamp: string list option
    NameMode: ResolvedNameMode
} with
    member this.ModuleContext =
        this.RenderContext.ModuleContext
    member this.GroupContext =
        this.RenderContext.GroupContext
    member this.InsideInterfaceOrStructStatic =
        // if there exists an interface or struct static group anywhere at or above a given level,
        // then some of the code generation is different, because we can't use simple namespaces anymore
        // eg functions need to be prefixed with "static", we use nested classes instead of namespaces for groups, etc
        match this.MaybePathClamp with
        | Some _ -> true
        | None -> false
    member this.NavigateToSub (name: string) =
        let nextRenderContext =
            this.RenderContext.NavigateToSub(name)
        let nextPathClamp =
            match this.MaybePathClamp with
            | Some value ->
                // already in, so just keep it
                Some value
            | None ->
                let nextIsInsideInterfaceOrStructStatic =
                    nextRenderContext.GroupContext.KindPath
                    |> List.exists (function | InterfaceStatic | StructStatic -> true | _ -> false)
                if nextIsInsideInterfaceOrStructStatic then
                    // next one is in a group where we can't follow (because we can't simply "namespace X {}" into interface or struct namespaces)
                    // so clamp the path at the current group, from which deeper names can be relatively prefixed
                    Some this.RenderContext.GroupContext.Path
                else
                    None
        { this with
            RenderContext = nextRenderContext
            MaybePathClamp = nextPathClamp }

let wrappersGroupSegmentRenameFunc (name: string) (prefix: GroupKind list) =
    let kind =
        prefix |> List.last
    let inFakeWrapperNamespace =
        prefix
        |> List.exists (function | InterfaceStatic | StructStatic -> true | _ -> false)
    match inFakeWrapperNamespace, kind with
    | true, _ -> name + "__"
    | _ -> name

let resolvedNameToString (context: CppRenderContext) =
    let pathClamp =
        match context.NameMode with
        | Normal | ImplementationHeader | ExtensionMethodsBlock ->
            None
        | Wrappers | WrappersPushPop ->
            // only in these modes do we pass it through
            context.MaybePathClamp
    let renameFunc =
        match context.NameMode with
        | WrappersPushPop ->
            wrappersGroupSegmentRenameFunc
        | _ ->
            (fun name _ -> name)
    let forceFullyQualified =
        // only .cpp files should use direct-import ambiguity checking (but I'm not sure this .NameMode maps 1:1 with the various file types, might need a new context field for FileType or something)
        match context.NameMode with
        | Normal -> NotForced
        | _ -> Forced [""] // a single empty item to force a prefix of ::
    let capsPolicy = {
        ModulePolicy = PascalCase
    }
    resolvedNameToStringAbstract "::" renameFunc pathClamp forceFullyQualified capsPolicy context.RenderContext

let constExprToString (context: CppRenderContext) =
    let config =
        { NullStr = "nullptr"
          ScopeResStr = "::" }
    let stringFunc resolvedName =
        resolvedNameToString context resolvedName TypeMode
    typedConstExprString config stringFunc

let getRelativePath (currentPath: string list) (targetPath: string list) =
    // remove common segments
    // prefix (currentPath.count) segments with ".."
    // then remainder of targetPath
    let current, target =
        (currentPath, targetPath)
        ||> List.skipWhile2 (fun a b -> a = b)
    List.replicate current.Length ".." @ target

let moduleNameToPathNoExt (current: ModuleName option) (target: ModuleName) =
    let currentParts =
        match current with
        | Some (ModuleName parts) -> parts
        | None -> []
    match target with
    | ModuleName targetParts ->
        let relative =
            getRelativePath currentParts targetParts
        String.concat "/" relative

type ModulePaths = {
    ModuleDir: string             // base/
    GeneratedDir: string          // base/generated/
    ModuleHeader: string          // base/generated/module.h
    ModuleWrappersHeader: string  // base/generated/module_wrappers.h
    ModuleWrappersCpp: string     // base/generated/module_wrappers.cpp
    ModuleCpp: string             // base/module.cpp
}

let getModulePaths (relativeTo: string list) (ModuleName nameParts) =
    let modulePath, name =
        List.splitAtLast nameParts
    let relativeRoot =
        getRelativePath relativeTo modulePath
    let relativeGenerated =
        getRelativePath relativeTo (modulePath @ ["generated"])
    { ModuleDir = joinNameParts "/" relativeRoot
      GeneratedDir = joinNameParts "/" relativeGenerated
      ModuleHeader = joinNameParts "/" (relativeGenerated @ [ name + ".h" ])
      ModuleWrappersHeader = joinNameParts "/" (relativeGenerated @ [ name + "_wrappers.h" ])
      ModuleWrappersCpp = joinNameParts "/" (relativeGenerated @ [ name + "_wrappers.cpp" ])
      ModuleCpp = joinNameParts "/" (relativeRoot @ [ name + ".cpp" ]) }

let typeToName (context: CppRenderContext) (outerType: NIType) =
    let rec recInner (inGeneric: bool) = function
        | UnitType -> "Unit"
        | Primitive prim ->
            match prim with
            | SizeT -> "SizeT"
            | Bool -> "Bool"
            | Int(signed, width) ->
                match signed, width with
                | true, Width8 -> "Int8"
                | false, Width8 -> "UInt8"
                | true, Width16 -> "Int16"
                | false, Width16 -> "UInt16"
                | true, Width32 -> "Int32"
                | false, Width32 -> "UInt32"
                | true, Width64 -> "Int64"
                | false, Width64 -> "UInt64"
            | Float32 -> "Float"
            | Float64 -> "Double"
            | StringType -> "String"
        | Resolved (resolvedName, _) ->
            resolvedNameToString context resolvedName (if inGeneric then GenericIdentifierMode else IdentifierMode)
        | ArrayType niType -> sprintf "%s_Array" (recInner true niType)
        | MapType (keyType, valueType) -> sprintf "%s_%s_Map" (recInner true keyType) (recInner true valueType)
        | TupleType tupleTypes -> sprintf "%s_Tuple" (tupleTypes |> List.map (recInner true) |> String.concat "")
        | SetType niType -> sprintf "%s_Set" (recInner true niType)
        | OptionType elem -> sprintf "%s_Option" (recInner true elem)
        | ResultType (succ, err) -> sprintf "%s_%s_Result" (recInner true succ) (recInner true err)
        | BufferType elemType -> sprintf "Native_%s_Buffer" (recInner true elemType)
        | x -> sprintf "(typeToName: not done yet [%A]" x
    recInner false outerType

type TypeContext =
    | Default
    | FunctionArguments
    | TypeParams
    | BareType  // ie, no std::shared_ptr<>, no Flags<T> etc
let typeToCppString (context: CppRenderContext) (outerTypeContext: TypeContext) (outerType: NIType) =
    let constWhenFuncArg (innerContext: TypeContext) (common: string) =
        match innerContext with
        | FunctionArguments ->
            sprintf "const %s&" common
        | _ ->
            common
    let rec recInner (innerContext: TypeContext) = function
        | UnitType -> "void"
        | Primitive prim ->
            match prim with
            | SizeT -> "size_t"
            | Bool -> "bool"
            | Int(signed, intWidth) ->
                match signed, intWidth with
                | true, Width8 -> "int8_t"
                | false, Width8 -> "uint8_t"
                | true, Width16 -> "int16_t"
                | false, Width16 -> "uint16_t"
                | true, Width32 -> "int32_t"
                | false, Width32 -> "uint32_t"
                | true, Width64 -> "int64_t"
                | false, Width64 -> "uint64_t"
            | Float32 -> "float"
            | Float64 -> "double"
            | StringType ->
                constWhenFuncArg innerContext "std::string"
        | Resolved (resolvedName, resolvedType) ->
            let name =
                resolvedNameToString context resolvedName TypeMode
            match resolvedType with
            | FwdOpaqueType ->
                // in the future we could do this properly, and emit the real symbol,
                // importing the pertinent .h files only in .cpp files (module.cpp, wrapper.cpp)
                // but for now we're just using an empty struct typedef that requires manual casting
                //
                // match requiredSymbolLookup context.ModuleContext resolvedName with
                // | FwdOpaqueDef parts ->
                //     sprintf "::%sRef" (joinNameParts "::" parts)
                // | _ ->
                //     failwithf "typeToCppString/Resolved/FwdOpaqueType - shouldn't happen"
                //
                sprintf "%sRef" name
            | OpaqueType ->
                sprintf "%sRef" name
            | InterfaceType ->
                match innerContext with
                | BareType ->
                    name
                | FunctionArguments ->
                    sprintf "const std::shared_ptr<%s>&" name
                | _ ->
                    sprintf "std::shared_ptr<%s>" name
            | DelegateType ->
                sprintf "std::function<%s>" name
                |> constWhenFuncArg innerContext
            | EnumType ->
                let definition =
                    requiredSymbolLookup context.ModuleContext resolvedName
                match definition with
                | EnumDef info ->
                    if info.FlagsMode then
                        sprintf "Flags<%s>" name
                    else
                        name
                | _ -> failwith "typeToCppString - problem fetching enum definition"
            | SumType ->
                sprintf "std::shared_ptr<%s::Base>" name
                |> constWhenFuncArg innerContext
            | OptionsType ->
                constWhenFuncArg innerContext name
            | NamedBitsType ->
                sprintf "Flags<%s>" name
            | StructType ->
                constWhenFuncArg innerContext name
            | TypedefType ->
                match innerContext with
                | FunctionArguments ->
                    // use const reference for certain types in a function arguments context
                    match requiredSymbolLookup context.ModuleContext resolvedName with
                    | TypeAliasDef actual ->
                        match actual with
                        // | UnitType -> failwith "todo"
                        // | Primitive prim -> failwith "todo"
                        // | ArrayType elem -> failwith "todo"
                        // | MapType(keyType, valueType) -> failwith "todo"
                        | TupleType _ ->
                            sprintf "const %s&" name
                        // | SetType elem -> failwith "todo"
                        // | BufferType elemType -> failwith "todo"
                        // | OptionType elem -> failwith "todo"
                        // | ResultType(success, error) -> failwith "todo"
                        // | Unresolved(identifier, loc, mustBe) -> failwith "todo"
                        // | Resolved(name, ``type``) -> failwith "todo"
                        | _ ->
                            name
                    | _ ->
                        failwithf "c++ defaultValueForType: Typedef lookup failed"
                | _ ->
                    // any other context:
                    name
            | _ ->
                name
        | ArrayType niType ->
            sprintf "std::vector<%s>" (recInner TypeParams niType)
            |> constWhenFuncArg innerContext
        | MapType (keyType, valueType) ->
            sprintf "std::map<%s,%s>" (recInner TypeParams keyType) (recInner TypeParams valueType)
            |> constWhenFuncArg innerContext
        | TupleType tupleTypes ->
            sprintf "std::tuple<%s>" (tupleTypes |> List.map (recInner TypeParams) |> String.concat ",")
            |> constWhenFuncArg innerContext
        | SetType niType ->
            sprintf "std::set<%s>" (recInner TypeParams niType)
            |> constWhenFuncArg innerContext
        | OptionType elem ->
            sprintf "std::optional<%s>" (recInner TypeParams elem)
            |> constWhenFuncArg innerContext
        | ResultType (succ, err) ->
            sprintf "std::expected<%s, %s>" (recInner TypeParams succ) (recInner TypeParams err)
            |> constWhenFuncArg innerContext
        | BufferType elemType ->
            sprintf "std::shared_ptr<NativeBuffer<%s>>" (recInner TypeParams elemType)
            |> constWhenFuncArg innerContext
        | x -> sprintf "(typeToCppString: not done yet [%A])" x
    recInner outerTypeContext outerType

let joinedFields (context: CppRenderContext) (fields: NamedField list) =
    [for field in fields -> sprintf "%s %s" (typeToCppString context Default field.Type_) field.Name]
    |> String.concat ", "

let joinedFieldNames (fields: NamedField list) =
    [for field in fields -> field.Name]
    |> String.concat ", "

let joinedArgs (context: CppRenderContext) (args: (string * NIType) list) =
    [for name, type_ in args -> sprintf "%s %s" (typeToCppString context FunctionArguments type_) name]
    |> String.concat ", "

let joinedNamedFuncArgs (context: CppRenderContext) (showDefaultValue: bool) (args: NamedFunctionArg list) =
    [for arg in args ->
        match arg.DefaultValue with
        | Some value ->
            if showDefaultValue then
                sprintf "%s %s = %s" (typeToCppString context FunctionArguments arg.Type) arg.Name (constExprToString context value)
            else
                sprintf "%s %s /* %s */" (typeToCppString context FunctionArguments arg.Type) arg.Name (constExprToString context value)
        | _ ->
            sprintf "%s %s" (typeToCppString context FunctionArguments arg.Type) arg.Name ]
    |> String.concat ", "

let joinedArgNames (args: (string * NIType) list) =
    args
    |> List.map fst
    |> String.concat ", "

let joinedNamedFuncArgNames (args: NamedFunctionArg list) =
    args
    |> List.map _.Name
    |> String.concat ", "


// super kludgy but super helpful!
// ideally we'd have a virtual-namespace-aware method of getting relative path prefixes of any symbol
// but right now doesn't seem worth the effort
let transformLastNameSegment (name: string) (f: string -> string) =
    let reversedParts =
        name.Split("::")
        |> Array.toList
        |> List.rev
    match reversedParts with
    | last :: etc ->
        (f last) :: etc
    | _ -> failwith "transformLastNameSegment fail"
    |> List.rev
    |> String.concat "::"

let rec defaultValueForType (context: CppRenderContext) (outerType: NIType) =
    let rec recInner = function
        | UnitType -> failwith "defaultValueForType called on UnitType"
        | Primitive prim ->
            match prim with
            | SizeT -> "0"
            | Bool -> "false"
            | Int _ -> "0"
            | Float32 -> "0.0f"
            | Float64 -> "0.0"
            | StringType -> "\"default\""
        | Resolved (resolvedName, resolvedType) ->
            let name =
                resolvedNameToString context resolvedName TypeMode
            match resolvedType with
            | FwdOpaqueType ->
                "nullptr"
            | OpaqueType ->
                // the final segment actually needs __ in front of it, since the opaque struct defs are defined with that
                // silly kludge for now, because AFAIK we don't need to directly deal with the default opaque structs (prefixed with __) anywhere else in the generation
                let replaced =
                    transformLastNameSegment name (fun seg -> "__" + seg)
                sprintf "new %s()" replaced
            | InterfaceType ->
                sprintf "std::shared_ptr<%s>()" name
            | DelegateType ->
                sprintf "std::function<%s>()" name
            | StructType ->
                sprintf "%s{}" name
            | OptionsType ->
                sprintf "%s{}" name
            | EnumType ->
                match requiredSymbolLookup context.ModuleContext resolvedName with
                | EnumDef info ->
                    if info.FlagsMode then
                        "0"
                    else
                        match info.Values with
                        | (valueName, _) :: _ -> sprintf "%s::%s" name valueName
                        | _ -> failwith "defaultValueForType: nope"
                | _ ->
                    failwithf "c++ defaultValueForType: EnumType lookup failed"
            | TypedefType ->
                match requiredSymbolLookup context.ModuleContext resolvedName with
                | TypeAliasDef actual ->
                    defaultValueForType context actual
                | _ ->
                    failwithf "c++ defaultValueForType: Typedef lookup failed"
            | _ ->
                "(defaultValueForType: not yet supported)"
        | ArrayType niType -> sprintf "std::vector<%s>()" (typeToCppString context TypeParams niType)
        | MapType (keyType, valueType) -> sprintf "std::map<%s,%s>()" (typeToCppString context TypeParams keyType) (typeToCppString context TypeParams valueType)
        | TupleType _ ->
            // let typesJoined =
            //     tupleTypes |> List.map (typeToCppString context TypeParams) |> String.concat ","
            // let defaultsJoined =
            //     tupleTypes |> List.map recInner |> String.concat ", "
            // sprintf "std::tuple<%s>{ %s }" typesJoined defaultsJoined
            "{}"
        | SetType niType -> sprintf "std::set<%s>()" (typeToCppString context TypeParams niType)
        | BufferType elemType -> sprintf "std::shared_ptr<NativeBuffer<%s>>()" (typeToCppString context TypeParams elemType)
        | OptionType elem -> sprintf "std::optional<%s>()" (typeToCppString context TypeParams elem)
        | ResultType (succ, _) -> sprintf "%s" (recInner succ)
        | _ ->
            "(defaultValueForType: not yet supported)"
    recInner outerType

type PopValueContext =
    | Variable of name: string
    | LeftAssign of leftSide: string
    | Return
    | SafeReturn
    | ExceptionContext
    | CastReturn of targetType: NIType

let popValue (context: CppRenderContext) (writer: IFormatWriter) (level: int) (type_: NIType) (popContext: PopValueContext) =
    let context =
        // enable "wrappers push/pop" mode if necessary
        // for rewriting path prefixes as usual :(((
        let nextNameMode =
            match context.NameMode with
            | Wrappers ->
                WrappersPushPop
            | _ ->
                context.NameMode
        { context with NameMode = nextNameMode }
    let write = writer.Write
    let contextPrefix =
        match popContext with
        | Variable varName -> sprintf "const auto %s =" varName
        | LeftAssign leftSide -> sprintf "%s = " leftSide
        | Return -> "return"
        | SafeReturn -> "const auto __ret ="
        | ExceptionContext -> ""
        | CastReturn _ -> "const auto __ret ="
    match type_ with
    | UnitType ->
        match popContext with
        | Return -> () // do nothing
        | _ -> failwith "tried to pop a Unit value in a non-Return context, why?"
    | Primitive prim ->
        match prim with
        | SizeT -> write level "%s ni_popSizeT();" contextPrefix
        | Bool -> write level "%s ni_popBool();" contextPrefix
        | Int(signed, width) ->
            write level "%s ni_pop%sInt%d();" contextPrefix (if signed then "" else "U") (widthToInt width)
        | Float32 -> write level "%s ni_popFloat();" contextPrefix
        | Float64 -> write level "%s ni_popDouble();" contextPrefix
        | StringType -> write level "%s popStringInternal();" contextPrefix
    | Resolved(resolvedName, resolvedType) ->
        let name =
            resolvedNameToString context resolvedName TypeMode
        match resolvedType with
        | ExceptionType ->
            write level "%s__buildAndThrow();" name
        | _ ->
            write level "%s %s__pop();" contextPrefix name
    | ArrayType niType ->
        let popFunc =
            match niType with
            | Primitive prim ->
                match prim with
                | SizeT -> "popSizeTArrayInternal"
                | Bool -> "popBoolArrayInternal"
                | Int(signed, width) ->
                    sprintf "pop%sInt%dArrayInternal" (if signed then "" else "U") (widthToInt width)
                | Float32 -> "popFloatArrayInternal"
                | Float64 -> "popDoubleArrayInternal"
                | StringType -> "popStringArrayInternal"
            | Resolved _ ->
                sprintf "__%s__pop" (typeToName context (ArrayType niType))
            | ArrayType _ -> sprintf "__%s__pop" (typeToName context (ArrayType niType))
            | TupleType _ -> sprintf "__%s__pop" (typeToName context (ArrayType niType))
            | SetType _ -> sprintf "__%s__pop" (typeToName context (ArrayType niType))
            | MapType _ -> sprintf "__%s__pop" (typeToName context (ArrayType niType))
            | OptionType _ -> sprintf "__%s__pop" (typeToName context (ArrayType niType))
            | ResultType _ -> sprintf "__%s__pop" (typeToName context (ArrayType niType))
            | _ -> sprintf "// (pop not yet supported: %A)" type_
        write level "%s %s();" contextPrefix popFunc
    | MapType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName context type_)
    | TupleType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName context type_)
    | SetType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName context type_)
    | BufferType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName context type_)
    | OptionType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName context type_)
    | ResultType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName context type_)
    | _ ->
        write level "// (pop not yet implemented: %A)" type_
    // epilogue
    match popContext with
    | SafeReturn ->
        write level "ni_clearClientSafetyArea();"
        write level "return __ret;"
    | CastReturn targetType ->
        write level "return static_cast<%s>(__ret);" (typeToCppString context TypeParams targetType)
    | _ ->
        ()

type PushIsReturn =
    | Direct of value: bool
    | Indirect of name: string
    | NotApplicable

let pushValue (context: CppRenderContext) (writer: IFormatWriter) (level: int) (type_: NIType) (content: string) (isReturn: PushIsReturn) =
    let write = writer.Write

    let context =
        // enable "wrappers push/pop" mode if necessary
        // for rewriting path prefixes as usual :(((
        let nextNameMode =
            match context.NameMode with
            | Wrappers ->
                WrappersPushPop
            | _ ->
                context.NameMode
        { context with NameMode = nextNameMode }

    let isReturnString() =
        match isReturn with
        | Direct value -> if value then "true" else "false"
        | Indirect name -> name
        | NotApplicable -> failwith "pushValue: 'NotApplicable' isReturn was consumed"

    let containsRefs =
        typeContainsRefs context.ModuleContext (NITypeRoot type_)

    match type_ with
    | UnitType ->
        write level "%s;" content // no push!
    | Primitive prim ->
        match prim with
        | SizeT -> write level "ni_pushSizeT(%s);" content
        | Bool -> write level "ni_pushBool(%s);" content
        | Int(signed, width) ->
            write level "ni_push%sInt%d(%s);" (if signed then "" else "U") (widthToInt width) content
        | Float32 -> write level "ni_pushFloat(%s);" content
        | Float64 -> write level "ni_pushDouble(%s);" content
        | StringType -> write level "pushStringInternal(%s);" content
    | Resolved (resolvedName, resolvedType) ->
        let name =
            resolvedNameToString context resolvedName TypeMode
        // switch on type for return param?
        match resolvedType with
        | ExceptionType ->
            assert (isReturn = Direct true)          // maybe needs a separate value for exceptions?
            write level "%s__push(%s);" name content // isreturn irrelevant, exceptions must push everything safely because they are basically returns
        | _ ->
            if containsRefs then
                write level "%s__push(%s, %s);" name content (isReturnString())
            else
                write level "%s__push(%s);" name content
    | ArrayType (Primitive prim) ->
        match prim with
        | SizeT -> write level "pushSizeTArrayInternal(%s);" content
        | Bool -> write level "pushBoolArrayInternal(%s);" content
        | Int(signed, intWidth) ->
            write level "push%sInt%dArrayInternal(%s);" (if signed then "" else "U") (widthToInt intWidth) content
        | Float32 -> write level "pushFloatArrayInternal(%s);" content
        | Float64 -> write level "pushDoubleArrayInternal(%s);" content
        | StringType -> write level "pushStringArrayInternal(%s);" content
    | _ ->
        if containsRefs then
            write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
        else
            write level "__%s__push(%s);" (typeToName context type_) content

let exceptionTypeAndVariableName (context: CppRenderContext) (resolvedName: ResolvedName) =
    let typeName =
        resolvedNameToString context resolvedName TypeMode
    // sigh @ getting the variable name
    // really need something cleaner than this
    let virtualName =
        // kind of an ugly kludge, we just need the group segments to be prefixed with __
        // since the namespaces in the wrappers file are "virtual" when inside of interface or struct statics
        let context =
            { context with NameMode = WrappersPushPop }
        resolvedNameToString context resolvedName TypeMode
    let variableName =
        transformLastNameSegment virtualName lowerFirst
    typeName, variableName
    // let parts =
    //     virtualName.Split("::")
    //     |> Array.toList
    //     |> List.rev
    // match parts with
    // | last :: revEtc ->
    //     let nsPrefix =
    //         List.rev revEtc
    //     let variableName =
    //         nsPrefix @ [ lowerFirst last ]
    //         |> String.concat "::"
    //     typeName, variableName
    // | [] ->
    //     failwith "exceptionVariableName: shouldn't happen"

let rec renderWrapperDelegate (context: CppRenderContext) (writerH: IFormatWriter) (writerCpp: IFormatWriter) (name: string) (info: DelegateInfo) (niType: NIType) =
    let writeCpp, blankCpp = writerCpp.Write, writerCpp.Blank
    let writeH, _ = writerH.Write, writerH.Blank

    // let typeString =
    //     typeToCppString context niType

    let rawDelegateType = // without the std::function<> shell
        match niType with
        | Resolved(resolvedName, DelegateType) ->
            resolvedNameToString context resolvedName TypeMode
        | _ ->
            failwith "c++ renderWrapperDelegate: uhhh"

    // push
    writeH 0 "void %s__push(%s f);" name (typeToCppString context FunctionArguments niType)
    writeCpp 0 "void %s__push(%s f) {" name (typeToCppString context FunctionArguments niType)
    writeCpp 1 "size_t uniqueKey = 0;"
    writeCpp 1 "if (f) {"
    writeCpp 2 "auto ptr_fun = f.target<%s>();" rawDelegateType
    writeCpp 2 "if (ptr_fun != nullptr) {"
    writeCpp 3 "uniqueKey = reinterpret_cast<size_t>(ptr_fun);"
    writeCpp 2 "}"
    writeCpp 1 "}"
    writeCpp 1 "auto wrapper = [f]() {"
    for argName, argType in info.Args do
        popValue context writerCpp 2 argType (Variable argName)
    let invocation =
        sprintf "f(%s)" (joinedArgNames info.Args)
    if info.Throws.IsEmpty then
        pushValue context writerCpp 2 info.Returns invocation (Direct true)
    else
        writeCpp 2 "try {"
        pushValue context writerCpp 3 info.Returns invocation (Direct true)
        writeCpp 2 "}"
        for exc in info.Throws do
            match exc with
            | Resolved (resolvedName, ExceptionType) ->
                let _, variableName =
                    exceptionTypeAndVariableName context resolvedName
                writeCpp 2 "catch (const %s& e) {" (typeToCppString context Default exc)
                writeCpp 3 "ni_setException(%s);" variableName
                pushValue context writerCpp 3 exc "e" (Direct true)
                writeCpp 2 "}"
            | _ ->
                failwithf "renderWrapperDelegate: 'throws' contained non exception type (%A)" exc
    writeCpp 1 "};"
    writeCpp 1 "pushServerFuncVal(wrapper, uniqueKey);"
    writeCpp 0 "}"
    // pop =============
    writeH 0 "%s %s__pop();" (typeToCppString context Default niType) name
    blankCpp()
    writeCpp 0 "%s %s__pop() {" (typeToCppString context Default niType) name
    writeCpp 1 "auto id = ni_popClientFunc();"
    writeCpp 1 "auto cf = std::make_shared<ClientFuncVal>(id);"
    writeCpp 1 "auto wrapper = [cf](%s) {" (joinedArgs context info.Args)
    for argName, argType in info.Args |> List.rev do
        pushValue context writerCpp 2 argType argName (Direct false)
    if info.Throws.IsEmpty then
        writeCpp 2 "cf->remoteExec();"
    else
        writeCpp 2 "cf->remoteExecWithExceptions();"
    if typeContainsRefs context.ModuleContext (NITypeRoot info.Returns) then
        popValue context writerCpp 2 info.Returns SafeReturn
    else
        popValue context writerCpp 2 info.Returns Return
    writeCpp 1 "};"
    writeCpp 1 "return wrapper;"
    writeCpp 0 "}"

// let relativeNamespacePrefixToTarget (context: CppRenderContext) (resolvedName: ResolvedName) =
//     // we want a prefix like "::Module::Group::etc::" that unambiguously points to the base of the symbol (but not the symbol itself)
//     // we can't use the usual resolvedNameToString because that's designed to refer to actual user-defined symbols, and its ambiguity checking will omit unnecessary prefixes
//     // whereas this is designed to preserve the fully qualified prefix, when we need to refer to some internal thing which doesn't participate in the usual symbol table ambiguity checking etc
//     // eg internal stuff like interface method refs
//     // right now this is used exclusively in renderWrapperInterface below, to point to inherited method refs
//     match resolvedName with
//     | ThisModule (SymbolName parts) ->
//         getRelativePath context.GroupContext.Path parts
//         |> List.allButLast
//         |> String.concat "::"
//     | Imported(ModuleName moduleParts, SymbolName symbolParts) ->
//         let x =
//             [""] @ moduleParts @ (symbolParts |> List.allButLast)
//             |> String.concat "::"
//         x + "::"

let typeWithLastPartFormat (context: CppRenderContext) (outerType: NIType) (format: string) =
    // function for appropriately-qualified path to some type, but to format the final part (interface name) however we want
    // eg. Module1.Group1.MyInterface -> Module1.Group1.ClientMyInterfaceImpl
    let x = typeToCppString context BareType outerType
    let parts = x.Split("::")
    let lastIndex = parts.Length - 1
    let formatThing =
        PrintfFormat<string -> string, unit, string, string>(format)
    parts[lastIndex] <- sprintf formatThing parts[lastIndex]
    String.concat "::" parts

let renderWrapperInterface (context: CppRenderContext) (writerH: IFormatWriter) (writerCpp: IFormatWriter) (outerType: NIType) (name: string) (info: InterfaceInfo) = // (typeContainsRefs: NIType -> bool) =
    let writeCpp, blankCpp = writerCpp.Write, writerCpp.Blank
    let writeH, blankH = writerH.Write, writerH.Blank

    // client<Whatever>Impl (composeable/inheritable interface "deltas" which implement the non-inherited methods)
    blankH()
    blankCpp()
    match info.MaybeParent with
    | Some parent ->
        let parentType =
            typeWithLastPartFormat context parent "Client%sImpl"
        let parentName =
            typeToName context parent
        writeH 0 "class Client%sImpl : public %s, public %s {" name parentType name
        writeH 0 "public:"
        writeH 0 "    explicit Client%sImpl(ClientObject *clientObject) : %s(clientObject) {}" name parentType
        writeH 0 "    %s* inherited_%s() override;" (typeToCppString context BareType parent) parentName
        // cpp version of parent getter
        writeCpp 0 "%s* Client%sImpl::inherited_%s() {" (typeToCppString context BareType parent) name parentName
        writeCpp 0 "    return this;"
        writeCpp 0 "}"
    | None ->
        writeH 0 "class Client%sImpl : public %s {" name name
        writeH 0 "protected:"
        writeH 0 "    ClientObject *clientObject;"
        writeH 0 "public:"
        writeH 0 "    explicit Client%sImpl(ClientObject *clientObject) : clientObject(clientObject) {}" name
    for namedFunc in info.Methods do
        let returnType =
            (typeToCppString context Default namedFunc.Returns)
        // header version ===================
        let arguments showDefaultArgs =
            joinedNamedFuncArgs context showDefaultArgs namedFunc.Args
        writeH 1 "%s %s(%s) override;" returnType namedFunc.Name (arguments true)
        // .cpp version =====================
        writeCpp 0 "%s Client%sImpl::%s(%s) {" returnType name namedFunc.Name (arguments false)
        // push args (reverse order)
        for arg in namedFunc.Args |> List.rev do
            pushValue context writerCpp 1 arg.Type arg.Name (Direct false)
        // invoke
        if namedFunc.Throws.IsEmpty then
            writeCpp 1 "clientObject->invokeMethod(%s_%s);" (lowerFirst name) namedFunc.Name
        else
            writeCpp 1 "clientObject->invokeMethodWithExceptions(%s_%s);" (lowerFirst name) namedFunc.Name
        // pop result
        if typeContainsRefs context.ModuleContext (NITypeRoot namedFunc.Returns) then
            // any sort of reference that might get freed if it was only a return value from the other side (potentially stored nowhere else, so protected by the safety area)
            // this is to prevent premature deref which could free the instance before we've gotten ahold of it
            popValue context writerCpp 1 namedFunc.Returns SafeReturn
        else
            popValue context writerCpp 1 namedFunc.Returns Return
        writeCpp 0 "}"
    // end class
    writeH 0 "};"

    // actual pushable ClientObject
    blankCpp()
    writeCpp 0 "class Client%sWrapper : public ClientObject, public Client%sImpl {" name name
    writeCpp 0 "public:"
    writeCpp 0 "    explicit Client%sWrapper(int id) : ClientObject(id), Client%sImpl(this) {}" name name
    writeCpp 0 "};"

    // PUSH ===================================
    blankH()
    writeH 0 "void %s__push(%s inst, bool isReturn);" (typeToName context outerType) (typeToCppString context FunctionArguments outerType)
    blankCpp()
    writeCpp 0 "void %s__push(%s inst, bool isReturn) {" (typeToName context outerType) (typeToCppString context FunctionArguments outerType)
    writeCpp 1 "if (inst != nullptr) {"
    writeCpp 2 "if (const auto pushable = std::dynamic_pointer_cast<Pushable>(inst); pushable != nullptr) {"
    writeCpp 3 "pushable->push(pushable, isReturn);"
    writeCpp 2 "} else {"
    writeCpp 3 "ni_clientLogger(LogLevel_Critical, \"%s__push(): could not cast to 'Pushable' - server-side interface implementations must inherit from ServerObject!\");" name
    writeCpp 3 "exit(-100);"
    writeCpp 2 "}"
    writeCpp 1 "} else {"
    writeCpp 2 "ni_pushNull();"
    writeCpp 1 "}"
    writeCpp 0 "}"

    // POP ====================================
    writeH 0 "%s %s__pop();" (typeToCppString context Default outerType) (typeToName context outerType)
    blankCpp()
    writeCpp 0 "%s %s__pop() {" (typeToCppString context Default outerType) (typeToName context outerType)
    writeCpp 1 "bool isClientID;"
    writeCpp 1 "if (auto id = ni_popInstance(&isClientID); id != 0) {"
    writeCpp 2 "if (isClientID) {"
    writeCpp 3 "return std::make_shared<Client%sWrapper>(id);" name
    writeCpp 2 "}"
    writeCpp 2 "return std::dynamic_pointer_cast<%s>(ServerObject::getByID(id));" name
    writeCpp 1 "}"
    writeCpp 1 "return {};"
    writeCpp 0 "}"

    // SERVER STUFF ============================
    for nf in info.Methods do
        blankH()
        writeH 0 "void %s_%s__wrapper(int serverID);" name nf.WrapperName
        blankCpp()
        writeCpp 0 "void %s_%s__wrapper(int serverID) {" name nf.WrapperName
        writeCpp 1 "const auto inst = std::dynamic_pointer_cast<%s>(ServerObject::getByID(serverID));" name
        for arg in nf.Args do
            popValue context writerCpp 1 arg.Type (Variable arg.Name)
        let invocation =
            sprintf "inst->%s(%s)" nf.Name (joinedNamedFuncArgNames nf.Args)
        if nf.Throws.IsEmpty then
            pushValue context writerCpp 1 nf.Returns invocation (Direct true)
        else
            writeCpp 1 "try {"
            pushValue context writerCpp 2 nf.Returns invocation (Direct true)
            writeCpp 1 "}"
            for exc in nf.Throws do
                match exc with
                | Resolved (resolvedName, ExceptionType) ->
                    let typeName, variableName =
                        exceptionTypeAndVariableName context resolvedName
                    writeCpp 1 "catch (const %s& e) {" typeName
                    writeCpp 2 "ni_setException(%s);" variableName
                    pushValue context writerCpp 2 exc "e" (Direct true) // writeCpp 2 "%s__push(e);" typeName
                    writeCpp 1 "}"
                | _ ->
                    failwithf "C++ interface wrappers: 'throws' contained non-exception type %A" exc
        writeCpp 0 "}"

let clampedNsPrefix (context: CppRenderContext) =
    match context.MaybePathClamp with
    | Some clampPath ->
        match List.removePrefix clampPath context.GroupContext.Path with
        | Some remaining ->
            (String.concat "::" remaining) + "::"
        | None ->
            // not a descendent of that prefix
            ""
    | None ->
        ""

let renderWrapperSumType (context: CppRenderContext) (writerH: IFormatWriter) (writerCpp: IFormatWriter) (name: string) (values: SumTypeValue list) (niType: NIType) =
    let writeCpp, blankCpp = writerCpp.Write, writerCpp.Blank
    let writeH, _ = writerH.Write, writerH.Blank

    let qualifiedName =
        let nsPrefix =
            clampedNsPrefix context
        nsPrefix + name

    let containsRefs =
        typeContainsRefs context.ModuleContext (NITypeRoot niType)
    let pushSuffix =
        if containsRefs then
            ", bool isReturn"
        else
            ""

    // push ========
    writeH 0 "void %s__push(%s value%s);" name (typeToCppString context FunctionArguments niType) pushSuffix
    // pushmatcher
    blankCpp()
    writeCpp 0 "class %s_PushMatcher : public %s::Matcher {" name qualifiedName
    writeCpp 0 "private:"

    if containsRefs then
        writeCpp 1 "bool isReturn;"
        writeCpp 0 "public:"
        writeCpp 1 "explicit %s_PushMatcher(bool isReturn) : isReturn(isReturn) {}" name
    else
        writeCpp 0 "public:"
        writeCpp 1 "%s_PushMatcher() = default;" name

    for i, value in values |> List.indexed do
        writeCpp 1 "void on%s(const %s::%s* %sValue) override {" value.Name qualifiedName value.Name (lowerFirst value.Name)
        for argName, argType in value.Args |> List.rev do
            let content =
                sprintf "%sValue->%s" (lowerFirst value.Name) argName
            pushValue context writerCpp 2 argType content (Indirect "isReturn")
        writeCpp 2 "// kind:"
        writeCpp 2 "ni_pushInt32(%d);" i
        writeCpp 1 "}"
    writeCpp 0 "};" // end pushmatcher
    // actual push
    blankCpp()
    writeCpp 0 "void %s__push(%s value%s) {" name (typeToCppString context FunctionArguments niType) pushSuffix
    if containsRefs then
        writeCpp 1 "%s_PushMatcher visitor(isReturn);" name
    else
        writeCpp 1 "%s_PushMatcher visitor;" name
    // writeCpp 1 "value->match((%s::Matcher*)&v);" qualifiedName // why this one with the casting?
    writeCpp 1 "value->match(&visitor);"
    writeCpp 0 "}"

    // pop =======
    writeH 0 "%s %s__pop();" (typeToCppString context Default niType) name
    blankCpp()
    writeCpp 0 "%s %s__pop() {" (typeToCppString context Default niType) name
    writeCpp 1 "%s::Base* __ret = nullptr;" qualifiedName
    writeCpp 1 "switch (ni_popInt32()) {"
    for i, value in values |> List.indexed do
        writeCpp 1 "case %d: {" i
        for argName, argType in value.Args do
            popValue context writerCpp 2 argType (Variable argName)
        writeCpp 2 "__ret = new %s::%s(%s);" qualifiedName value.Name (joinedArgNames value.Args)
        writeCpp 2 "break;"
        writeCpp 1 "}"
    writeCpp 1 "default:"
    writeCpp 2 "ni_clientLogger(LogLevel_Error, \"C++ %s__pop() - unknown kind! returning null\");" name
    writeCpp 1 "}" // end switch
    writeCpp 1 "return std::shared_ptr<%s::Base>(__ret);" qualifiedName
    writeCpp 0 "}"

type ArrayOrSetMode =
    | ArrayMode
    | SetMode
type ArrayOrSetRenderer(context: CppRenderContext, writer: IFormatWriter, outerType: NIType, elemType: NIType, mode: ArrayOrSetMode) =
    // TODO! this doesn't yet support .H file writing
    // but then do we ever really need that? aren't all generics contained within the wrapper .cpp file?
    // and rendered in strict dependency order? would the .H be required to avoid some kind of circularity?
    let write, blank = writer.Write, writer.Blank

    let pushSuffix =
        if typeContainsRefs context.ModuleContext (NITypeRoot outerType) then
            ", bool isReturn"
        else
            ""

    // use this for any types with no particular specializations (typedefs, delegates, arrays of arrays -- everything but structs and tuples, basically)
    let arrayDefault () =
        // PUSH ========
        write 0 "void __%s__push(%s values%s) {" (typeToName context outerType) (typeToCppString context FunctionArguments outerType) pushSuffix
        write 1 "for (auto v = values.rbegin(); v != values.rend(); v++) {" // in reverse (for arrays - for sets, order doesn't matter)
        pushValue context writer 2 elemType "*v" (Indirect "isReturn")
        write 1 "}"
        write 1 "ni_pushSizeT(values.size());"
        write 0 "}"
        // POP =========
        blank()
        write 0 "%s __%s__pop() {" (typeToCppString context Default outerType) (typeToName context outerType)
        write 1 "%s __ret;" (typeToCppString context Default outerType)
        write 1 "auto count = ni_popSizeT();"
        write 1 "for (auto i = 0; i < count; i++) {"
        popValue context writer 2 elemType (Variable "value")
        match mode with
        | ArrayMode ->
            write 2 "__ret.push_back(value);"
        | SetMode ->
            write 2 "__ret.insert(value);"
        write 1 "}"
        write 1 "return __ret;"
        write 0 "}"
    interface IArrayLikeRenderer with
        member this.TypedefTuple (tt: NIType) =
            match mode with
            | ArrayMode ->
                // push
                write 0 "inline void __%s__push(%s values%s) {" (typeToName context outerType) (typeToCppString context FunctionArguments outerType) pushSuffix
                pushValue context writer 1 (ArrayType tt) "values" (Indirect "isReturn")
                write 0 "}"
                // pop
                blank()
                write 0 "inline %s __%s__pop() {" (typeToCppString context Default outerType) (typeToName context outerType)
                popValue context writer 1 (ArrayType tt) Return
                write 0 "}"
            | SetMode ->
                // push
                write 0 "inline void __%s__push(%s set%s) {" (typeToName context outerType) (typeToCppString context FunctionArguments outerType) pushSuffix
                pushValue context writer 1 (SetType tt) "set" (Indirect "isReturn")
                write 0 "}"
                // pop
                blank()
                write 0 "inline %s __%s__pop() {" (typeToCppString context Default outerType) (typeToName context outerType)
                popValue context writer 1 (SetType tt) Return
                write 0 "}"
        member this.TypedefPrim (prim: NIPrimitive) =
            // push
            write 0 "inline void __%s__push(%s values%s) {" (typeToName context outerType) (typeToCppString context FunctionArguments outerType) pushSuffix
            pushValue context writer 1 (ArrayType (Primitive prim)) "values" (Indirect "isReturn")
            write 0 "}"
            // pop
            blank()
            write 0 "inline %s __%s__pop() {" (typeToCppString context Default outerType) (typeToName context outerType)
            popValue context writer 1 (ArrayType (Primitive prim)) Return
            write 0 "}"
        member this.EnumType (info: EnumInfo) =
            let widthType =
                widthTypeForEnum context.ModuleContext info
            if info.FlagsMode then
                // PUSH ===================================
                write 0 "inline void __%s__push(%s values%s) {" (typeToName context outerType) (typeToCppString context FunctionArguments outerType) pushSuffix
                pushValue context writer 1 (ArrayType widthType) "values" NotApplicable
                write 0 "}"
                // POP =====================================
                write 0 "inline %s __%s__pop() {" (typeToCppString context Default outerType) (typeToName context outerType)
                popValue context writer 1  (ArrayType widthType) PopValueContext.Return
                write 0 "}"
            else
                // PUSH ================
                write 0 "void __%s__push(%s values%s) {" (typeToName context outerType) (typeToCppString context FunctionArguments outerType) pushSuffix
                write 1 "std::vector<%s> intValues;" (typeToCppString context TypeParams widthType)
                write 1 "for (auto i : values) {"
                write 2 "intValues.push_back(static_cast<%s>(i));" (typeToCppString context Default widthType)
                write 1 "}"
                pushValue context writer 1 (ArrayType widthType) "intValues" (Indirect "isReturn")
                write 0 "}"
                // POP ==================
                blank()
                write 0 "%s __%s__pop() {" (typeToCppString context Default outerType) (typeToName context outerType)
                popValue context writer 1 (ArrayType widthType) (Variable "intValues")
                write 1 "%s __ret;" (typeToCppString context Default outerType)
                write 1 "for (auto i : intValues) {"
                match mode with
                | ArrayMode ->
                    write 2 "__ret.push_back(static_cast<%s>(i));" (typeToCppString context TypeParams elemType)
                | SetMode ->
                    write 2 "__ret.insert(static_cast<%s>(i));" (typeToCppString context TypeParams elemType)
                write 1 "}"
                write 1 "return __ret;"
                write 0 "}"
        member this.StructType (structName: ResolvedName) (namedFields: NamedField list) =
            let typeStr =
                resolvedNameToString context structName TypeMode
            // PUSH ===========
            write 0 "void __%s__push(%s values%s) {" (typeToName context outerType) (typeToCppString context FunctionArguments outerType) pushSuffix
            for field in namedFields |> List.rev do
                write 1 "std::vector<%s> %s_values;" (typeToCppString context TypeParams field.Type_) field.Name
            write 1 "for (auto v = values.begin(); v != values.end(); v++) {"
            for field in namedFields |> List.rev do
                write 2 "%s_values.push_back(v->%s);" field.Name field.Name
            write 1 "}"
            for field in namedFields |> List.rev do
                pushValue context writer 1 (ArrayType field.Type_) (sprintf "%s_values" field.Name) (Indirect "isReturn")
            write 0 "}"
            // POP ============
            blank()
            write 0 "%s __%s__pop() {" (typeToCppString context Default outerType) (typeToName context outerType)
            for field in namedFields do
                popValue context writer 1 (ArrayType field.Type_) (Variable (sprintf "%s_values" field.Name))
            write 1 "%s __ret;" (typeToCppString context Default outerType)
            // use first field to iterate
            write 1 "for (auto i = 0; i < %s_values.size(); i++) {" namedFields.Head.Name
            write 2 "%s __value;" typeStr
            for field in namedFields do
                write 2 "__value.%s = %s_values[i];" field.Name field.Name
            match mode with
            | ArrayMode ->
                write 2 "__ret.push_back(__value);"
            | SetMode ->
                write 2 "__ret.insert(__value);"
            write 1 "}"
            write 1 "return __ret;"
            write 0 "}"
        member this.TupleType (tupleTypes: NIType list) =
            // PUSH ========
            write 0 "void __%s__push(%s values%s) {" (typeToName context outerType) (typeToCppString context FunctionArguments outerType) pushSuffix
            for i, type_ in tupleTypes |> List.indexed do
                write 1 "std::vector<%s> _%dvalues;" (typeToCppString context TypeParams type_) i
            write 1 "for (auto tv : values) {"
            for i, _ in tupleTypes |> List.indexed do
                write 2 "_%dvalues.push_back(std::get<%d>(tv));" i i
            write 1 "}"
            // push in reverse field order
            for i, type_ in tupleTypes |> List.indexed |> List.rev do
                pushValue context writer 1 (ArrayType type_) (sprintf "_%dvalues" i) (Indirect "isReturn")
            write 0 "}"
            // POP =========
            blank()
            write 0 "%s __%s__pop() {" (typeToCppString context Default outerType) (typeToName context outerType)
            for i, type_ in tupleTypes |> List.indexed do
                popValue context writer 1 (ArrayType type_) (Variable (sprintf "_%dvalues" i))
            write 1 "%s __ret;" (typeToCppString context Default outerType)
            write 1 "for (auto i = 0; i < _0values.size(); i++) {"
            let joined =
                tupleTypes |> List.indexed |> List.map fst |> List.map (sprintf "_%dvalues[i]") |> String.concat ", "
            match mode with
            | ArrayMode ->
                write 2 "__ret.push_back({ %s });" joined
            | SetMode ->
                write 2 "__ret.insert({ %s });" joined
            write 1 "}"
            write 1 "return __ret;"
            write 0 "}"
        member this.ArrayType () =
            arrayDefault()
        member this.MapType () =
            arrayDefault()
        member this.DelegateType () =
            arrayDefault()
        member this.SetType () =
            arrayDefault()
        member this.OptionType elemType =
            // push ==========
            write 0 "void __%s__push(%s values%s) {" (typeToName context outerType) (typeToCppString context FunctionArguments outerType) pushSuffix
            write 1 "%s hasValueArray;" (typeToCppString context Default (ArrayType (Primitive Bool)))
            write 1 "hasValueArray.reserve(values.size());"
            write 1 "%s valueArray;" (typeToCppString context Default (ArrayType elemType))
            write 1 "for (auto i = values.begin(); i != values.end(); i++) {"
            write 2 "if (i->has_value()) {"
            write 3 "valueArray.push_back(i->value());"
            write 3 "hasValueArray.push_back(true);"
            write 2 "}"
            write 2 "else {"
            write 3 "hasValueArray.push_back(false);"
            write 2 "}"
            write 1 "}"
            pushValue context writer 1 (ArrayType elemType) "valueArray" (Indirect "isReturn")
            pushValue context writer 1 (ArrayType (Primitive Bool)) "hasValueArray" NotApplicable
            write 0 "}"
            // pop ===========
            blank()
            write 0 "%s __%s__pop() {" (typeToCppString context Default outerType) (typeToName context outerType)
            popValue context writer 1 (ArrayType (Primitive Bool)) (Variable "hasValueArray")
            popValue context writer 1 (ArrayType elemType) (Variable "valueArray")
            write 1 "%s ret(hasValueArray.size());" (typeToCppString context Default outerType) // allocate w/ default values = None
            write 1 "auto valuePtr = valueArray.data();"
            write 1 "for (auto i = 0; i < hasValueArray.size(); i++) {"
            write 2 "if (hasValueArray[i]) {"
            write 3 "ret[i] = *valuePtr++;"
            write 2 "}"
            write 1 "}"
            write 1 "return ret;"
            write 0 "}"
        member this.ResultType succType errType =
            // push =========
            write 0 "void __%s__push(%s values%s) {" (typeToName context outerType) (typeToCppString context FunctionArguments outerType) pushSuffix
            write 1 "%s isSuccessArray;" (typeToCppString context Default (ArrayType (Primitive Bool)))
            write 1 "isSuccessArray.reserve(values.size());"
            write 1 "%s successValuesArray;" (typeToCppString context Default (ArrayType succType))
            write 1 "%s errorValuesArray;" (typeToCppString context Default (ArrayType errType))
            write 1 "for (auto i = values.begin(); i != values.end(); i++) {"
            write 2 "if (i->has_value()) {"
            write 3 "successValuesArray.push_back(*i);"
            write 3 "isSuccessArray.push_back(true);"
            write 2 "}"
            write 2 "else {"
            write 3 "errorValuesArray.push_back(i->error());"
            write 3 "isSuccessArray.push_back(false);"
            write 2 "}"
            write 1 "}"
            pushValue context writer 1 (ArrayType errType) "errorValuesArray" (Indirect "isReturn")
            pushValue context writer 1 (ArrayType succType) "successValuesArray" (Indirect "isReturn")
            pushValue context writer 1 (ArrayType (Primitive Bool)) "isSuccessArray" NotApplicable
            write 0 "}"
            // pop ==========
            blank()
            write 0 "%s __%s__pop() {" (typeToCppString context Default outerType) (typeToName context outerType)
            popValue context writer 1 (ArrayType (Primitive Bool)) (Variable "isSuccessArray")
            popValue context writer 1 (ArrayType succType) (Variable "successValuesArray")
            popValue context writer 1 (ArrayType errType) (Variable "errorValuesArray")
            write 1 "%s ret;" (typeToCppString context Default outerType)
            write 1 "ret.reserve(isSuccessArray.size());"
            write 1 "auto succPtr = successValuesArray.data();"
            write 1 "auto errPtr = errorValuesArray.data();"
            write 1 "for (auto i = isSuccessArray.begin(); i != isSuccessArray.end(); i++) {"
            write 2 "if (*i) {"
            write 3 "ret.push_back(*succPtr++);"
            write 2 "}"
            write 2 "else {"
            write 3 "ret.push_back(std::unexpected(*errPtr++));"
            write 2 "}"
            write 1 "}"
            write 1 "return ret;"
            write 0 "}"
        member this.SumType () =
            arrayDefault()
        member this.Primitive (_: NIPrimitive) =
            // primitve arrays are built in, but not sets!
            match mode with
            | ArrayMode ->
                write 0 "// built-in array type: %s" (typeToCppString context Default outerType)
            | SetMode ->
                // push ==============
                write 0 "void __%s__push(%s items) {" (typeToName context outerType) (typeToCppString context FunctionArguments outerType)
                write 1 "auto itemsArr = %s(items.begin(), items.end());" (typeToCppString context Default (ArrayType elemType))
                pushValue context writer 1 (ArrayType elemType) "itemsArr" NotApplicable
                write 0 "}"
                // pop ===============
                blank()
                write 0 "%s __%s__pop() {" (typeToCppString context Default outerType) (typeToName context outerType)
                popValue context writer 1 (ArrayType elemType) (Variable "itemsArr")
                write 1 "return %s(itemsArr.begin(), itemsArr.end());" (typeToCppString context Default outerType)
                write 0 "}"
        member this.Opaque () =
            match mode with
            | ArrayMode ->
                // push
                write 0 "void __%s__push(%s items) {" (typeToName context outerType) (typeToCppString context FunctionArguments outerType)
                write 1 "auto data = const_cast<%s*>(items.data());" (typeToCppString context Default elemType)
                write 1 "ni_pushPtrArray(reinterpret_cast<void**>(data), items.size());"
                write 0 "}"
                // pop
                blank()
                write 0 "%s __%s__pop() {" (typeToCppString context Default outerType) (typeToName context outerType)
                write 1 "%s *values;" (typeToCppString context Default elemType)
                write 1 "size_t count;"
                write 1 "ni_popPtrArray(reinterpret_cast<void***>(&values), &count);"
                write 1 "return { values, values + count };"
                write 0 "}"
            | SetMode ->
                write 0 "void __%s__push(%s items) {" (typeToName context outerType) (typeToCppString context FunctionArguments outerType)
                write 1 "std::vector vec(items.begin(), items.end());"
                write 1 "ni_pushPtrArray(reinterpret_cast<void**>(vec.data()), vec.size());"
                write 0 "}"
                // pop
                blank()
                write 0 "%s __%s__pop() {" (typeToCppString context Default outerType) (typeToName context outerType)
                write 1 "%s *values;" (typeToCppString context Default elemType)
                write 1 "size_t count;"
                write 1 "ni_popPtrArray(reinterpret_cast<void***>(&values), &count);"
                write 1 "return { values, values + count };"
                write 0 "}"
        member this.Options () =
            arrayDefault()
        member this.Other (_: NIType) =
            write 2 "// TODO: push/pop for array-like of %s" (typeToCppString context Default outerType)

let synthesizeOpaqueMethods (context: CppRenderContext) (opaqueName: string) (info: OpaqueInfo) =
    let fakeThis =
        let symbolName =
            SymbolName (context.GroupContext.Path @ [ opaqueName ])
        Resolved ((ThisModule symbolName), OpaqueType)
    let instanceMethods =
        if info.NoDispose then
            info.Methods
        else
            let disposeMethod =
                { Name = "dispose"; Args = []; Returns = UnitType; Throws = []; MaybeOverloadId = None; ShadowsExisting = false } // hmm, shouldn't this be 'true' in some cases? but then maybe it doesn't matter on the C++ side ...
            (info.Methods @ [ disposeMethod ]) // seems better at the end of instance methods
        // add "_this" parameter
        |> List.map (fun nf ->
            let nextArgs =
                let firstArg =
                    { Name = "_this"; Type = fakeThis; DefaultValue = None }
                firstArg :: nf.Args
            { nf with Args = nextArgs })
    // name prefix
    instanceMethods
    |> List.map (fun nf ->
        let nextName =
            sprintf "%s_%s" opaqueName nf.Name
        { nf with Name = nextName })

let synthesizeExtensionMethods (context: CppRenderContext) (info: ExtMethodsInfo) =
    let context =
        // force name rendering to be absolute!
        // otherwise it's inconsistent between implementation .cpp and other places :(
        { context with NameMode = ExtensionMethodsBlock }
    info.Methods
    |> List.map (fun nf ->
        // name prefix
        let nextName =
            sprintf "ext_%s_%s" (typeToName context info.Target) nf.Name
        // inject 'this' arg
        let nextArgs =
            let firstArg =
                { Name = "_this"; Type = info.Target; DefaultValue = None }
            firstArg :: nf.Args
        { nf with Name = nextName; Args = nextArgs })

let renderWrapperNamedFunc (context: CppRenderContext) (writerH: IFormatWriter) (writerCpp: IFormatWriter) (namedFunction: NamedFunction) =
    let writeCpp, blankCpp = writerCpp.Write, writerCpp.Blank
    let writeH, blankH = writerH.Write, writerH.Blank

    let nsPrefix =
        // clampedNsPrefix context
        //
        // quick hack to get fully-qualified method invocations, to solve a problem:
        // if this fails to work, or we keep having problems, perhaps a better long-term solution is
        // to remove all the "using namespace" directives, and fully qualify all imports in generated files, and even in the initial uendited .cpp file
        // (the developer can optionally write their own "using" in the .cpp file alone to reduce visual noise)
        let moduleParts =
            match context.ModuleContext.Module.Name with
            | ModuleName parts -> parts
        let otherParts =
            context.GroupContext.Path
        let joined =
            (moduleParts @ otherParts)
            |> String.concat "::"
        "::" + joined + "::"

    blankH()
    writeH 0 "void %s__wrapper();" namedFunction.WrapperName
    blankCpp()
    writeCpp 0 "void %s__wrapper() {" namedFunction.WrapperName
    for arg in namedFunction.Args do
        popValue context writerCpp 1 arg.Type (Variable arg.Name)
    let invocation =
        sprintf "%s%s(%s)" nsPrefix namedFunction.Name (joinedNamedFuncArgNames namedFunction.Args)
    if namedFunction.Throws.IsEmpty then
        pushValue context writerCpp 1 namedFunction.Returns invocation (Direct true)
    else
        writeCpp 1 "try {"
        pushValue context writerCpp 2 namedFunction.Returns invocation (Direct true)
        writeCpp 1 "}"
        for exc in namedFunction.Throws do
            match exc with
            | Resolved (resolvedName, ExceptionType) ->
                let typeName, variableName =
                    exceptionTypeAndVariableName context resolvedName
                writeCpp 1 "catch (const %s& e) {" typeName
                writeCpp 2 "ni_setException(%s);" variableName
                pushValue context writerCpp 2 exc "e" (Direct true)
                writeCpp 1 "}"
            | _ ->
                failwithf "C++ plain function wrappers: 'throws' contained non-exception type"
    writeCpp 0 "}"

let renderWrapperGeneric (context: CppRenderContext) (writerH: IFormatWriter) (writerCpp: IFormatWriter) (gType: NIType) =
    let writeCpp, blankCpp = writerCpp.Write, writerCpp.Blank
    let writeH, blankH = writerH.Write, writerH.Blank

    let pushSuffix =
        if typeContainsRefs context.ModuleContext (NITypeRoot gType) then
            ", bool isReturn"
        else
            ""

    match gType with
    | BufferType elemType ->
        // PUSH =====
        blankH()
        writeH 0 "inline void __%s__push(%s buf, bool isReturn);" (typeToName context gType) (typeToCppString context FunctionArguments gType)
        writeCpp 0 "inline void __%s__push(%s buf, bool isReturn) {" (typeToName context gType) (typeToCppString context FunctionArguments gType)
        writeCpp 1 "if (buf != nullptr) {"
        writeCpp 2 "auto pushable = std::dynamic_pointer_cast<Pushable>(buf);"
        writeCpp 2 "pushable->push(pushable, isReturn);"
        writeCpp 1 "}"
        writeCpp 1 "else {"
        writeCpp 2 "ni_pushNull();"
        writeCpp 1 "}"
        writeCpp 0 "}"
        // POP ======
        writeH 0 "%s __%s__pop();" (typeToCppString context Default gType) (typeToName context gType)
        blankCpp()
        writeCpp 0 "%s __%s__pop() {" (typeToCppString context Default gType) (typeToName context gType)
        writeCpp 1 "int id;"
        writeCpp 1 "bool isClientId;"
        writeCpp 1 "ni_BufferDescriptor descriptor;"
        writeCpp 1 "ni_popBuffer(&id, &isClientId, &descriptor);"
        writeCpp 1 "if (id != 0) {"
        writeCpp 2 "if (isClientId) {"
        writeCpp 3 "auto buf = new ClientBuffer<%s>(id, descriptor);" (typeToCppString context TypeParams elemType)
        writeCpp 3 "return %s(buf);" (typeToCppString context Default gType)
        writeCpp 2 "}"
        writeCpp 2 "else {"
        writeCpp 3 "return ServerBuffer<%s>::getByID(id);" (typeToCppString context TypeParams elemType)
        writeCpp 2 "}"
        writeCpp 1 "}"
        writeCpp 1 "else {"
        writeCpp 2 "return %s();" (typeToCppString context Default gType)
        writeCpp 1 "}"
        writeCpp 0 "}"
    | TupleType tupleTypes ->
        // PUSH =====
        blankH()
        writeH 0 "void __%s__push(%s value%s);" (typeToName context gType) (typeToCppString context FunctionArguments gType) pushSuffix
        writeCpp 0 "void __%s__push(%s value%s) {" (typeToName context gType) (typeToCppString context FunctionArguments gType) pushSuffix
        for i, type_ in tupleTypes |> List.indexed |> List.rev do
            pushValue context writerCpp 1 type_ (sprintf "std::get<%d>(value)" i) (Indirect "isReturn")
        writeCpp 0 "}"
        // POP ======
        writeH 0 "%s __%s__pop();" (typeToCppString context Default gType) (typeToName context gType)
        blankCpp()
        writeCpp 0 "%s __%s__pop() {" (typeToCppString context Default gType) (typeToName context gType)
        for i, type_ in tupleTypes |> List.indexed do
            popValue context writerCpp 1 type_ (Variable (sprintf "_%d" i))
        let itemsJoined =
            tupleTypes
            |> List.indexed
            |> List.map (fun (i, _) -> sprintf "_%d" i)
            |> String.concat ", "
        writeCpp 1 "return %s{ %s };" (typeToCppString context Default gType) itemsJoined
        writeCpp 0 "}"
    | MapType (keyType, valueType) ->
        // PUSH =====
        blankH()
        writeH 0 "void __%s__push(%s _map%s);" (typeToName context gType) (typeToCppString context FunctionArguments gType) pushSuffix
        writeCpp 0 "void __%s__push(%s _map%s) {" (typeToName context gType) (typeToCppString context FunctionArguments gType) pushSuffix
        writeCpp 1 "std::vector<%s> keys;" (typeToCppString context TypeParams keyType)
        writeCpp 1 "std::vector<%s> values;" (typeToCppString context TypeParams valueType)
        writeCpp 1 "for (auto i = _map.begin(); i != _map.end(); i++) {"
        writeCpp 2 "keys.push_back(i->first);"
        writeCpp 2 "values.push_back(i->second);"
        writeCpp 1 "}"
        pushValue context writerCpp 1 (ArrayType valueType) "values" (Indirect "isReturn")
        pushValue context writerCpp 1 (ArrayType keyType) "keys" (Indirect "isReturn")
        writeCpp 0 "}"
        // POP =====
        writeH 0 "%s __%s__pop();" (typeToCppString context Default gType) (typeToName context gType)
        blankCpp()
        writeCpp 0 "%s __%s__pop() {" (typeToCppString context Default gType) (typeToName context gType)
        writeCpp 1 "%s __ret;" (typeToCppString context Default gType)
        popValue context writerCpp 1 (ArrayType keyType) (Variable "keys")
        popValue context writerCpp 1 (ArrayType valueType) (Variable "values")
        writeCpp 1 "for (auto i = 0; i < keys.size(); i++) {"
        writeCpp 2 "__ret[keys[i]] = values[i];"
        writeCpp 1 "}"
        writeCpp 1 "return __ret;"
        writeCpp 0 "}"
    | ArrayType niType ->
        let renderer =
            ArrayOrSetRenderer(context, writerCpp, gType, niType, ArrayMode)
        renderArrayLike context.ModuleContext niType renderer
    | SetType niType ->
        let renderer =
            ArrayOrSetRenderer(context, writerCpp, gType, niType, SetMode)
        renderArrayLike context.ModuleContext niType renderer
    | ResultType (succType, errType) ->
        // PUSH =====
        blankH()
        blankCpp()
        writeH 0 "void __%s__push(%s value%s);" (typeToName context gType) (typeToCppString context FunctionArguments gType) pushSuffix
        writeCpp 0 "void __%s__push(%s value%s) {" (typeToName context gType) (typeToCppString context FunctionArguments gType) pushSuffix
        writeCpp 1 "if (value.has_value()) {"
        pushValue context writerCpp 2 succType "*value" (Indirect "isReturn")
        pushValue context writerCpp 2 (Primitive Bool) "true" NotApplicable
        writeCpp 1 "}"
        writeCpp 1 "else {"
        pushValue context writerCpp 2 errType "value.error()" (Indirect "isReturn")
        pushValue context writerCpp 2 (Primitive Bool) "false" NotApplicable
        writeCpp 1 "}"
        writeCpp 0 "}"
        // PUSH =====
        writeH 0 "%s __%s__pop();" (typeToCppString context Default gType) (typeToName context gType)
        blankCpp()
        writeCpp 0 "%s __%s__pop() {" (typeToCppString context Default gType) (typeToName context gType)
        popValue context writerCpp 1 (Primitive Bool) (Variable "isSuccess")
        writeCpp 1 "if (isSuccess) {"
        popValue context writerCpp 2 succType (Variable "successValue")
        writeCpp 2 "return successValue;"
        writeCpp 1 "}"
        popValue context writerCpp 1 errType (Variable "errValue")
        writeCpp 1 "return std::unexpected(errValue);"
        writeCpp 0 "}"
    | OptionType elem ->
        // PUSH =====
        blankH()
        blankCpp()
        writeH 0 "void __%s__push(%s value%s);" (typeToName context gType) (typeToCppString context FunctionArguments gType) pushSuffix
        writeCpp 0 "void __%s__push(%s value%s) {" (typeToName context gType) (typeToCppString context FunctionArguments gType) pushSuffix
        writeCpp 1 "if (value.has_value()) {"
        pushValue context writerCpp 2 elem "value.value()" (Indirect "isReturn")
        pushValue context writerCpp 2 (Primitive Bool) "true" NotApplicable
        writeCpp 1 "}"
        writeCpp 1 "else {"
        pushValue context writerCpp 2 (Primitive Bool) "false" NotApplicable
        writeCpp 1 "}"
        writeCpp 0 "}"
        // POP =====
        writeH 0 "%s __%s__pop();" (typeToCppString context Default gType) (typeToName context gType)
        blankCpp()
        writeCpp 0 "%s __%s__pop() {" (typeToCppString context Default gType) (typeToName context gType)
        writeCpp 1 "%s maybeValue;" (typeToCppString context Default gType)
        popValue context writerCpp 1 (Primitive Bool) (Variable "hasValue")
        writeCpp 1 "if (hasValue) {"
        popValue context writerCpp 2 elem (LeftAssign "maybeValue")
        writeCpp 1 "}"
        writeCpp 1 "return maybeValue;"
        writeCpp 0 "}"
    | _ ->
        writeCpp 0 "// push/pop for generic %s" (typeToCppString context Default gType)


let rec renderWrapperUserType (context: CppRenderContext) (writerH: IFormatWriter) (writerCpp: IFormatWriter) (name: string) (userType: UserType) =
    let writeCpp, blankCpp = writerCpp.Write, writerCpp.Blank
    let writeH, blankH = writerH.Write, writerH.Blank

    // necessary to have as an NIType in many places
    // especially since we can't actually dive into namespaces for interface/struct statics,
    // and have to rely on typeToCppString to emit path prefixes
    let niType =
        let symbolName =
            SymbolName (context.GroupContext.Path @ [ name ])
        let resolvedType =
            match userType with
            | FwdOpaqueDef _ -> FwdOpaqueType
            | OpaqueDef _ -> OpaqueType
            | StructDef _ -> StructType
            | ExceptionDef _ -> ExceptionType
            | InterfaceDef _ -> InterfaceType
            | DelegateDef _ -> DelegateType
            | TypeAliasDef _ -> TypedefType
            | EnumDef _ -> EnumType
            | NamedBitsDef _ -> NamedBitsType
            | SumTypeDef _ -> SumType
            | OptionsDef _ -> OptionsType
        Resolved (ThisModule symbolName, resolvedType)

    let pushSuffix =
        if typeContainsRefs context.ModuleContext (UserTypeRoot userType) then
            ", bool isReturn"
        else
            ""

    match userType with
    | FwdOpaqueDef _ ->
        // if we ever start referring to the real symbol (see note in typeToCppString),
        // we would remove this and just use the already-existing push/pop functions
        blankH()
        writeH 0 "void %s__push(%s value);" name (typeToCppString context FunctionArguments niType)
        writeCpp 0 "void %s__push(%s value) {" name (typeToCppString context FunctionArguments niType)
        writeCpp 1 "ni_pushPtr(value);"
        writeCpp 0 "}"
        blankCpp()
        writeH 0 "%s %s__pop();" (typeToCppString context Default niType) name
        writeCpp 0 "%s %s__pop() {" (typeToCppString context Default niType) name
        writeCpp 1 "return (%s)ni_popPtr();" (typeToCppString context Default niType)
        writeCpp 0 "}"
    | OpaqueDef info ->
        blankH()
        writeH 0 "void %s__push(%s value);" name (typeToCppString context FunctionArguments niType)
        writeCpp 0 "void %s__push(%s value) {" name (typeToCppString context FunctionArguments niType)
        writeCpp 1 "ni_pushPtr(value);"
        writeCpp 0 "}"
        blankCpp()
        writeH 0 "%s %s__pop();" (typeToCppString context Default niType) name
        writeCpp 0 "%s %s__pop() {" (typeToCppString context Default niType) name
        writeCpp 1 "return static_cast<%s>(ni_popPtr());" (typeToCppString context Default niType)
        writeCpp 0 "}"
        // nested static block
        match info.MaybeStaticDefs with
        | Some _ ->
            let nextContext =
                context.NavigateToSub(name)
            if context.InsideInterfaceOrStructStatic then
                writeH 0 "namespace %s__ {" name
                writeCpp 0 "namespace %s__ {" name
            else
                writeH 0 "namespace %s {" name
                writeCpp 0 "namespace %s {" name
            //
            renderWrappersGroup nextContext (writerH.WithExtraIndent 1) (writerCpp.WithExtraIndent 1)
            //
            writeH 0 "}"
            writeCpp 0 "}"
        | None ->
            ()
        // methods
        for nf in synthesizeOpaqueMethods context name info do
            renderWrapperNamedFunc context writerH writerCpp nf
    | StructDef info ->
        blankH()
        writeH 0 "void %s__push(%s value%s);" name (typeToCppString context FunctionArguments niType) pushSuffix
        writeCpp 0 "void %s__push(%s value%s) {" name (typeToCppString context FunctionArguments niType) pushSuffix
        for field in info.Fields |> List.rev do
            pushValue context writerCpp 1 field.Type_ (sprintf "value.%s" field.Name) (Indirect "isReturn")
        writeCpp 0 "}"
        blankCpp()
        writeH 0 "%s %s__pop();" (typeToCppString context Default niType) name
        writeCpp 0 "%s %s__pop() {" (typeToCppString context Default niType) name
        for field in info.Fields do
            popValue context writerCpp 1 field.Type_ (Variable field.Name)
        writeCpp 1 "return %s { %s };" (typeToCppString context Default niType) (joinedFieldNames info.Fields)
        writeCpp 0 "}"
        // nested static block
        match info.MaybeStaticDefs with
        | Some _ ->
            let nextContext =
                context.NavigateToSub(name)
            writeH 0 "namespace %s__ {" name
            writeCpp 0 "namespace %s__ {" name
            //
            renderWrappersGroup nextContext (writerH.WithExtraIndent 1) (writerCpp.WithExtraIndent 1)
            //
            writeH 0 "}"
            writeCpp 0 "}"
        | None ->
            ()
    | InterfaceDef info ->
        // nested static block
        match info.MaybeStaticDefs with
        | Some _ ->
            let nextContext =
                context.NavigateToSub(name)
            writeH 0 "namespace %s__ {" name
            writeCpp 0 "namespace %s__ {" name
            //
            renderWrappersGroup nextContext (writerH.WithExtraIndent 1) (writerCpp.WithExtraIndent 1)
            //
            writeH 0 "}"
            writeCpp 0 "}"
        | None ->
            ()
        renderWrapperInterface context writerH writerCpp niType name info
    | OptionsDef namedFields ->
        let flagsType =
            Primitive(Int(true,Width32))
        // push
        blankH()
        writeH 0 "void %s__push(%s value%s);" name (typeToCppString context FunctionArguments niType) pushSuffix
        writeCpp 0 "void %s__push(%s value%s) {" name (typeToCppString context FunctionArguments niType) pushSuffix
        for field in namedFields |> List.rev do
            writeCpp 1 "%s %s;" (typeToCppString context Default field.Type_) field.Name
            writeCpp 1 "if (value.has%s(&%s)) {" (upperFirst field.Name) field.Name
            pushValue context writerCpp 2 field.Type_ field.Name (Indirect "isReturn")
            writeCpp 1 "}"
        pushValue context writerCpp 1 flagsType "value.getUsedFields()" NotApplicable
        writeCpp 0 "}"
        // pop
        blankCpp()
        writeH 0 "%s %s__pop();" (typeToCppString context Default niType) name
        writeCpp 0 "%s %s__pop() {" (typeToCppString context Default niType) name
        writeCpp 1 "%s value = {};" (typeToCppString context Default niType)
        popValue context writerCpp 1 flagsType (LeftAssign "value._usedFields")
        for field in namedFields do
            writeCpp 1 "if (value._usedFields & %s::Fields::%sField) {" (typeToCppString context Default niType) (upperFirst field.Name)
            popValue context writerCpp 2 field.Type_ (Variable "x")
            writeCpp 2 "value.set%s(x);" (upperFirst field.Name)
            writeCpp 1 "}"
        writeCpp 1 "return value;"
        writeCpp 0 "}"
    | ExceptionDef exFields ->
        blankH()
        writeH 0 "void %s__push(%s e);" name (typeToCppString context FunctionArguments niType)
        blankCpp()
        writeCpp 0 "void %s__push(%s e) {" name (typeToCppString context FunctionArguments niType)
        // push field values (reverse order)
        for namedField in exFields |> List.rev do
            let memberVar =
                sprintf "e.%s" namedField.Name
            pushValue context writerCpp 1 namedField.Type_ memberVar (Direct true) // true = exceptions are always returned by definition, so any references in their fields need to be kept safe ... right?
        writeCpp 0 "}"
        // build and throw
        writeH 0 "void %s__buildAndThrow();" name
        blankCpp()
        writeCpp 0 "void %s__buildAndThrow() {" name
        for field in exFields do
            popValue context writerCpp 1 field.Type_ (Variable field.Name)
        // joined variables (_0, _1, etc)
        writeCpp 1 "throw %s(%s);" (typeToCppString context Default niType) (joinedFieldNames exFields)
        writeCpp 0 "}"
    | DelegateDef info ->
        renderWrapperDelegate context writerH writerCpp name info niType
    | TypeAliasDef niType ->
        // sadly it turns out we cannot inline these in the wrapper header file, which seemed the obvious thing to do
        // because we encountered a case where a typedef wrapped a generic, but generic push/pop functions only get rendered to the wrapper .cpp
        // and typedefs DO need to be pushed/popped by name from other modules, and we can't just flatten them because if they map to a generic, the push/pop for that only exists in the module containing the typdef->generic
        // long story short, this needs to be a normal, real function present in both the header and cpp. ah well!
        // push
        blankH()
        blankCpp()
        writeH 0 "void %s__push(%s value%s);" name name pushSuffix
        writeCpp 0 "void %s__push(%s value%s) {" name name pushSuffix
        pushValue context writerCpp 1 niType "value" (Indirect "isReturn")
        writeCpp 0 "}"
        // pop
        blankH()
        blankCpp()
        writeH 0 "%s %s__pop();" name name
        writeCpp 0 "%s %s__pop() {" name name // (typeToCppString context Default niType) name
        popValue context writerCpp 1 niType Return
        writeCpp 0 "}"
    | EnumDef info ->
        let widthType =
            widthTypeForEnum context.ModuleContext info
        if info.FlagsMode then
            blankH()
            writeH 0 "void %s__push(%s value);" name (typeToCppString context FunctionArguments niType)
            writeCpp 0 "void %s__push(%s value) {" name (typeToCppString context FunctionArguments niType)
            pushValue context writerCpp 1 widthType "value.value()" NotApplicable
            writeCpp 0 "}"
            writeH 0 "%s %s__pop();" (typeToCppString context Default niType) name
            blankCpp()
            writeCpp 0 "%s %s__pop() {" (typeToCppString context Default niType) name
            popValue context writerCpp 1 widthType (CastReturn niType)
            writeCpp 0 "}"
        else
            blankH()
            writeH 0 "void %s__push(%s value);" name (typeToCppString context FunctionArguments niType)
            writeCpp 0 "void %s__push(%s value) {" name (typeToCppString context FunctionArguments niType)
            let content =
                sprintf "static_cast<%s>(value)" (typeToCppString context Default widthType)
            pushValue context writerCpp 1 widthType content NotApplicable
            writeCpp 0 "}"
            writeH 0 "%s %s__pop();" (typeToCppString context Default niType) name
            blankCpp()
            writeCpp 0 "%s %s__pop() {" (typeToCppString context Default niType) name
            popValue context writerCpp 1 widthType (CastReturn niType)
            writeCpp 0 "}"
    | NamedBitsDef info ->
        let _, widthType =
            let resolver resolvedName =
                resolvedNameToString context resolvedName TypeMode
            namedBitsIndexer info context.ModuleContext resolver name
        blankH()
        writeH 0 "void %s__push(%s value);" name (typeToCppString context FunctionArguments niType)
        writeCpp 0 "void %s__push(%s value) {" name (typeToCppString context FunctionArguments niType)
        pushValue context writerCpp 1 widthType "value.value()" NotApplicable
        writeCpp 0 "}"
        writeH 0 "%s %s__pop();" (typeToCppString context Default niType) name
        blankCpp()
        writeCpp 0 "%s %s__pop() {" (typeToCppString context Default niType) name
        popValue context writerCpp 1 widthType (CastReturn niType)
        writeCpp 0 "}"
    | SumTypeDef values ->
        renderWrapperSumType context writerH writerCpp name values niType

and renderWrappersGroup (context: CppRenderContext) (writerH: IFormatWriter) (writerCpp: IFormatWriter) =
    let writeCpp, _ = writerCpp.Write, writerCpp.Blank
    let writeH, _ = writerH.Write, writerH.Blank
    let group =
        context.GroupContext

    // declare various module handles etc at top of group
    for name, info in group.Interfaces do
        for method in info.Methods do
            writeCpp 0 "ni_InterfaceMethodRef %s_%s;" (lowerFirst name) method.WrapperName

    for name, _ in group.Exceptions do
        writeCpp 0 "ni_ExceptionRef %s;" (lowerFirst name)

    // go!
    for def in group.Defs do
        match def with
        | Global _ ->
            ()
        | FunctionDef namedFunction ->
            renderWrapperNamedFunc context writerH writerCpp namedFunction
        | ExtMethodsBlock info ->
            let targetTypeStr =
                typeToCppString context Default info.Target
            writeH 0 "// ext methods block for: %s" targetTypeStr
            writeCpp 0 "// ext methods block for: %s" targetTypeStr
            for func in synthesizeExtensionMethods context info do
                renderWrapperNamedFunc context writerH writerCpp func
        | UserType(name, userType) ->
            renderWrapperUserType context writerH writerCpp name userType
        | Group(groupName, _) ->
            let nextContext =
                context.NavigateToSub(groupName)
            if context.InsideInterfaceOrStructStatic then
                writeH 0 "namespace %s__ {" groupName
                writeCpp 0 "namespace %s__ {" groupName
            else
                writeH 0 "namespace %s {" groupName
                writeCpp 0 "namespace %s {" groupName
            renderWrappersGroup nextContext (writerH.WithExtraIndent 1) (writerCpp.WithExtraIndent 1)
            writeH 0 "}"
            writeCpp 0 "}"

// used below to import only those wrappers required by any interfaces in this module,
// which inherit others
let computeInheritedInterfaceModules (module_: Module) =
    let userTypeItems (thing: UserType) =
        match thing with
        | InterfaceDef info ->
            match info.MaybeParent with
            | Some (Resolved (resolvedName, _)) ->
                match resolvedName with
                | Imported (moduleName, _) ->
                    [ moduleName ]
                | ThisModule _ ->
                    []
            | _ ->
                // something other than a resolved parent
                []
        | _ ->
            // non-interface type
            []
    let rec recInner (table: SymbolTable) =
        table
        |> Map.toList
        |> List.collect (fun (_, entry) ->
            match entry with
            | SymbolTableEntry.Direct thing ->
                userTypeItems thing
            | SubTable table ->
                recInner table
            | Hybrid(thing, table) ->
                let thingItems = userTypeItems thing
                let tableItems = recInner table
                thingItems @ tableItems
            )
    match module_.SymbolTable with
    | Complete table ->
        recInner table
        |> List.distinct
    | _ ->
        failwith "x"

let generateWrappers (context: CppRenderContext) (writerH: IFormatWriter) (writerCpp: IFormatWriter) (currentDir: string list) = // (maybeModulePrefix: string option) (className: string) =
    let writeCpp, blankCpp = writerCpp.Write, writerCpp.Blank
    let writeH, blankH = writerH.Write, writerH.Blank
    let m =
        context.ModuleContext.Module

    // change the resolved name mode, so that it's emitting the correct prefixes from the clamp point
    // (because the common resolved name computation has no concept of path clamping, which is currently unique to C++ rendering)
    // see resolvedNameToString to see what we do differently in 'Wrappers' mode
    let context =
        { context with NameMode = Wrappers }

    // begin output: ===============================================
    // header
    writeH 0 "#pragma once"
    writeH 0 "#include \"%s.h\"" m.Name.LastPart

    // figure out which XXXX_wrapper.h files need to be imported even in the header here,
    // because we are doing inheritance with Client<whatever>Impl classes
    // if we have problems with this in the future, I guess we'll move such things to a separate XXXX_interfaces.h file
    let inheritedInterfaceModules =
        computeInheritedInterfaceModules context.ModuleContext.Module
    if not inheritedInterfaceModules.IsEmpty then
        blankH()
        writeH 0 "// wrapper.h files required by this module's interfaces (via inheritance):"
        for inherited in inheritedInterfaceModules do
            let importPaths =
                getModulePaths currentDir inherited
            writeH 0 "#include \"%s\"" importPaths.ModuleWrappersHeader
            // match inherited with
            // | ModuleName nameParts ->
            //     writeH 0 "using namespace ::%s;" (joinNameParts "::" nameParts)

    // cpp
    writeCpp 0 "#include \"%s.h\"" (moduleNameToPathNoExt (Some m.Name) (ModuleName ["support"; "NativeImplServer"])) // (relativeIncludeFilenameNoExt maybeModulePrefix "$PARENT.support.NativeImplServer")
    writeCpp 0 "#include \"%s_wrappers.h\"" m.Name.LastPart
    writeCpp 0 "#include \"%s.h\"" m.Name.LastPart
    // need to include wrappers for imports
    for importName in m.Imports do
        blankCpp()
        let importPaths =
            getModulePaths currentDir importName
        writeCpp 0 "#include \"%s\"" importPaths.ModuleWrappersHeader
        // match importName with
        // | ModuleName nameParts ->
        //     writeCpp 0 "using namespace ::%s;" (joinNameParts "::" nameParts)

    // begin root group: ===========================================
    blankH();
    blankCpp()
    match m.Name with
    | ModuleName nameParts ->
        writeH 0 "namespace %s" (joinNameParts "::" nameParts)
        writeCpp 0 "namespace %s" (joinNameParts "::" nameParts)
    writeH 0 "{"
    writeCpp 0 "{"

    // first render all generic stuff in the root group
    for gType in allGenericTypes context.ModuleContext do
        renderWrapperGeneric context (writerH.WithExtraIndent 1) (writerCpp.WithExtraIndent 1) gType

    // then the group content
    renderWrappersGroup context (writerH.WithExtraIndent 1) (writerCpp.WithExtraIndent 1)

    // globals/constants func
    let allGlobalsCount =
        countGroupConstants context.GroupContext

    if allGlobalsCount > 0 then
        blankCpp()
        writeCpp 1 "void __constantsFunc() {"
        // push all subgroups at once
        let rec pushGroupConstants (group: GroupContext) =
            for subName in group.SubOrder |> List.rev do
                let sub = group.SubGroups[subName]
                pushGroupConstants sub
            for globalName, globalType in group.Globals |> List.rev do
                let content =
                    joinNameParts "::" (group.Path @ [ globalName ])
                pushValue context writerCpp 2 globalType content (Direct false) // hmm for isReturn? I guess not because the variable is global and not going anywhere, so it doesn't need to be in the safe area
        pushGroupConstants context.GroupContext
        writeCpp 1 "}"

    // library registration method
    blankH()
    writeH 1 "int __register();"
    blankCpp()
    writeCpp 1 "int __register() {"
    writeCpp 2 "auto m = ni_registerModule(\"%s\");" m.Name.LastPart
    if allGlobalsCount > 0 then
        writeCpp 2 "ni_registerModuleConstants(m, &__constantsFunc);"

    // register all nested group methods/interfaces/etc
    let rec registerGroupMethods (group: GroupContext) (writer: IFormatWriter) =
        // apply any renaming rules to the group path segments
        let groupPathRenamed =
            (group.Path, group.KindPath)
            ||> List.zip
            |> renameGroupPath wrappersGroupSegmentRenameFunc
            // ||> List.map2 groupSegmentRenameFuncWrappersFunc

        // module methods (+ synthesized opaque methods, + extension methods)
        let opaqueMethods =
            group.Opaques
            |> List.collect (fun (name, info) -> synthesizeOpaqueMethods context name info)
        let extMethods =
            group.ExtMethodBlocks
            |> List.collect (synthesizeExtensionMethods context)
        let allMethods =
            group.ModuleMethods @ opaqueMethods @ extMethods
        for namedFunction in allMethods do
            let parts =
                group.Path @ [ namedFunction.WrapperName ]
            let renamedParts =
                groupPathRenamed @ [ namedFunction.WrapperName ]
            writeCpp 2 "ni_registerModuleMethod(m, \"%s\", &%s__wrapper);" (joinNameParts "." parts) (joinNameParts "::" renamedParts)
        // interfaces + methods
        for name, info in group.Interfaces do
            let lowerIfaceName =
                lowerFirst name
            let parts =
                group.Path @ [ name ]
            let renamedParts =
                groupPathRenamed @ [ name ]
            let lowerParts =
                groupPathRenamed @ [ lowerIfaceName ]
            writeCpp 2 "auto %s = ni_registerInterface(m, \"%s\");" lowerIfaceName (joinNameParts "." parts)
            for method in info.Methods do
                let targetVarName =
                    sprintf "%s_%s" (joinNameParts "::" lowerParts) method.WrapperName
                let wrapperName =
                    sprintf "%s_%s__wrapper" (joinNameParts "::" renamedParts) method.WrapperName
                writeCpp 2 "%s = ni_registerInterfaceMethod(%s, \"%s\", &%s);" targetVarName lowerIfaceName method.WrapperName wrapperName
        // exceptions
        for name, _ in group.Exceptions do
            let parts =
                group.Path @ [ name ]
            let renamedParts =
                groupPathRenamed @ [ name ]
            let lowerName =
                lowerFirst name
            let assignTo =
                let prefix =
                    if groupPathRenamed.IsEmpty then ""
                    else (joinNameParts "::" groupPathRenamed) + "::"
                prefix + lowerName
            writeCpp 2 "%s = ni_registerException(m, \"%s\", &%s__buildAndThrow);" assignTo (joinNameParts "." parts) (joinNameParts "::" renamedParts)
        // subgroups
        for subName in group.SubOrder do
            let sub = group.SubGroups[subName]
            registerGroupMethods sub writer
    registerGroupMethods context.GroupContext writerCpp
    writeCpp 2 "return 0; // = OK"
    writeCpp 1 "}"

    // end root group: ===========================================
    writeH 0 "}"
    writeCpp 0 "}"

let renderHeaderException (context: CppRenderContext) (writer: IFormatWriter) (exName: string) (exFields: NamedField list) =
    let write, _ = writer.Write, writer.Blank
    //
    write 0 "class %s {" exName
    write 0 "public:"
    for namedField in exFields do
        write 1 "%s %s;" (typeToCppString context Default namedField.Type_) namedField.Name
    if exFields.IsEmpty then
        write 1 "%s() {}" exName
    else
        let joinedInitializers =
            [for nf in exFields -> sprintf "%s(%s)" nf.Name nf.Name]
            |> String.concat ", "
        write 1 "%s(%s)" exName (joinedFields context exFields)
        write 2 ": %s {}" joinedInitializers
    write 0 "};"

let renderHeaderSumType (context: CppRenderContext) (writer: IFormatWriter) (sumTypeName: string) (values: SumTypeValue list) =
    let write, blank = writer.Write, writer.Blank
    //
    if context.InsideInterfaceOrStructStatic then
        write 0 "class %s {" sumTypeName
        write 0 "public:"
    else
        write 0 "namespace %s {" sumTypeName
    // fwd declarations for case classes
    // Base still needs to be emitted at group level to allow for mutual recursion
    for value in values do
        write 1 "class %s;" value.Name

    blank()
    write 1 "class Matcher {"
    write 1 "public:"
    for value in values do
        write 2 "virtual void on%s(const %s* value) = 0;" value.Name value.Name // (lowerFirst value.Name)
    write 1 "};"

    blank()
    write 1 "class Base {"
    write 1 "public:"
    write 2 "virtual void match(Matcher* matcher) = 0;"
    write 1 "};"

    for value in values do
        blank()
        write 1 "class %s : public Base {" value.Name
        write 1 "public:"
        for name, type_ in value.Args do
            write 2 "const %s %s;" (typeToCppString context Default type_) (lowerFirst name)
        let initializersString =
            let raw =
                value.Args
                |> List.map (fun (name, _) -> sprintf "%s(%s)" name name)
                |> String.concat ", "
            if raw.Length > 0 then
                " : " + raw
            else
                ""
        write 2 "%s(%s)%s {}" value.Name (joinedArgs context value.Args) initializersString
        write 2 "void match(Matcher* matcher) override {"
        write 3 "matcher->on%s(this);" value.Name
        write 2 "}" // end match()
        write 1 "};" // end value class

    if context.InsideInterfaceOrStructStatic then
        write 0 "};" // end class
    else
        write 0 "}"  // end namespace

let renderHeaderOptionsType (context: CppRenderContext) (writer: IFormatWriter) (userType: UserType) (optionsTypeName: string) (namedFields: NamedField list) =
    let write, _ = writer.Write, writer.Blank

    let pushSuffix =
        if typeContainsRefs context.ModuleContext (UserTypeRoot userType) then
            ", bool isReturn"
        else
            ""

    write 0 "struct %s {" optionsTypeName
    write 0 "private:"
    write 1 "enum Fields {"
    for (i, field), maybeComma in namedFields |> List.indexed |> withTrailingCommas do
        write 2 "%sField = %d%s" (upperFirst field.Name) (1 <<< i) maybeComma
    write 1 "};"
    let usedFieldsType =
        Primitive (Int(true, Width32))
    write 1 "%s _usedFields = %s;" (typeToCppString context Default usedFieldsType) (defaultValueForType context usedFieldsType)
    for field in namedFields do
        write 1 "%s _%s;" (typeToCppString context Default field.Type_) field.Name
    write 0 "protected:"
    write 1 "[[nodiscard]] %s getUsedFields() const {" (typeToCppString context Default usedFieldsType)
    write 2 "return _usedFields;"
    write 1 "}"
    write 1 "friend void %s__push(const %s& value%s);" (upperFirst optionsTypeName) (upperFirst optionsTypeName) pushSuffix
    write 1 "friend %s %s__pop();" (upperFirst optionsTypeName) (upperFirst optionsTypeName)
    write 0 "public:"
    // accessor methods
    for field in namedFields do
        write 1 "void set%s(%s value) {" (upperFirst field.Name) (typeToCppString context FunctionArguments field.Type_)
        write 2 "_%s = value;" field.Name
        write 2 "_usedFields |= Fields::%sField;" (upperFirst field.Name)
        write 1 "}"
        write 1 "bool has%s(%s *value) const {" (upperFirst field.Name) (typeToCppString context Default field.Type_)
        write 2 "if (_usedFields & Fields::%sField) {" (upperFirst field.Name)
        write 3 "*value = _%s;" field.Name
        write 3 "return true;"
        write 2 "}"
        write 2 "return false;"
        write 1 "}"
        write 1 "[[nodiscard]] %s getOrDefault%s(%s defaultValue) const {" (typeToCppString context Default field.Type_) (upperFirst field.Name) (typeToCppString context FunctionArguments field.Type_)
        write 2 "if (_usedFields & Fields::%sField) {" (upperFirst field.Name)
        write 3 "return _%s;" field.Name
        write 2 "}"
        write 2 "return defaultValue;"
        write 1 "}"
        write 1 "[[nodiscard]] auto mapOrDefault%s(auto mapFunc, auto defaultValue) const {" (upperFirst field.Name)
        write 2 "if (_usedFields & Fields::%sField) {" (upperFirst field.Name)
        write 3 "return mapFunc(_%s);" field.Name
        write 2 "}"
        write 2 "return defaultValue;"
        write 1 "}"
    // done
    write 0 "};"

let rec renderHeaderUserType (context: CppRenderContext) (writer: IFormatWriter) (name: string) (userType: UserType) =
    let write, blank = writer.Write, writer.Blank

    blank()

    match userType with
    | FwdOpaqueDef _ ->
        // nothing to emit here, we do it alongside normal fwd opaque declarations at the top of the group
        ()
    | OpaqueDef info ->
        match info.MaybeStaticDefs with
        | Some _ ->
            write 0 "namespace %s {" name
            let nextContext =
                context.NavigateToSub(name)
            renderImplementationHeaderGroup nextContext (writer.WithExtraIndent 1)
            write 0 "}" // end namespace
        | None ->
            ()
        // methods
        let staticPrefix =
            if context.InsideInterfaceOrStructStatic then
                "static "
            else
                ""
        let synthesized =
            synthesizeOpaqueMethods context name info
        for namedFunc in synthesized do
            let throwsComment =
                if namedFunc.Throws.IsEmpty then
                    ""
                else
                    let joined =
                        namedFunc.Throws
                        |> List.map (typeToCppString context Default)
                        |> String.concat ", "
                    sprintf " // throws %s" joined
            write 0 "%s%s %s(%s);%s" staticPrefix (typeToCppString context Default namedFunc.Returns) namedFunc.Name (joinedNamedFuncArgs context true namedFunc.Args) throwsComment
    | StructDef info ->
        let renderFields (context: CppRenderContext) =
            for field in info.Fields do
                write 1 "%s %s;" (typeToCppString context Default field.Type_) field.Name
        write 0 "struct %s {" name
        match info.MaybeStaticDefs with
        | Some _ ->
            let nextContext =
                context.NavigateToSub(name)
            write 1 "// static definitions:"
            renderImplementationHeaderGroup nextContext (writer.WithExtraIndent 1)
            write 1 "// fields:"
            renderFields nextContext
        | None ->
            renderFields context
        // default comparison handling (for sets etc):
        write 1 "auto operator<=>(const %s&) const = default;" name
        write 0 "};" // end struct
    | OptionsDef namedFields ->
        renderHeaderOptionsType context writer userType name namedFields
    | ExceptionDef namedFields ->
        renderHeaderException context writer name namedFields
    | InterfaceDef info ->
        let renderMethods (context: CppRenderContext) =
            for namedFunc in info.Methods do
                let throwsComment =
                    if namedFunc.Throws.IsEmpty then
                        ""
                    else
                        let joined =
                            namedFunc.Throws
                            |> List.map (typeToCppString context Default)
                            |> String.concat ", "
                        sprintf " // throws %s" joined
                write 1 "virtual %s %s(%s) = 0;%s" (typeToCppString context Default namedFunc.Returns) namedFunc.Name (joinedNamedFuncArgs context true namedFunc.Args) throwsComment
        // class/interface proper
        write 0 "class %s {" name
        write 0 "public:"
        write 0 "    virtual ~%s() = default;" name
        match info.MaybeParent with
        | Some parent ->
            // direct parent only:
            write 1 "virtual %s* inherited_%s() = 0;" (typeToCppString context BareType parent) (typeToName context parent)
            // TODO: convenience casts for entire inheritance chain?
            write 1 "static %s as_%s(const std::shared_ptr<%s> &outerThis) {" (typeToCppString context Default parent) (typeToName context parent) name
            write 1 "    return std::dynamic_pointer_cast<%s>(outerThis);" (typeToCppString context BareType parent)
            write 1 "}"
        | None ->
            write 1 "// no parent interface"
        // static defs ... hmm, untested
        match info.MaybeStaticDefs with
        | Some _ ->
            let nextContext =
                context.NavigateToSub(name)
            write 1 "// static definitions:"
            renderImplementationHeaderGroup nextContext (writer.WithExtraIndent 1)
            write 1 "// instance methods:"
            renderMethods nextContext
        | None ->
            renderMethods context
        write 0 "};"
    | DelegateDef info ->
        let maybeThrows =
            if info.Throws.IsEmpty then
                ""
            else
                let joined =
                    info.Throws
                    |> List.map (typeToCppString context Default)
                    |> String.concat ", "
                sprintf " // throws %s" joined
        write 0 "typedef %s %s(%s);%s" (typeToCppString context Default info.Returns) name (joinedArgs context info.Args) maybeThrows
    | TypeAliasDef actual ->
        write 0 "typedef %s %s;" (typeToCppString context Default actual) name
    | EnumDef info ->
        let widthType =
            widthTypeForEnum context.ModuleContext info
        let widthSuffix =
            match widthType with
            | Primitive(Int(true, Width32)) -> ""
            | _ -> sprintf " : %s" (typeToCppString context Default widthType)
        let emitValues indent =
            // this function originally existed because we used it in multiple places,
            // but we can probably inline it in the future
            for (value, maybeSpecifiedValue), maybeComma in info.Values |> withTrailingCommas do
                match maybeSpecifiedValue with
                | Some specValue ->
                    let enumValueStr =
                        constExprToString context specValue
                    write indent "%s = %s%s" value enumValueStr maybeComma
                | None ->
                    write indent "%s%s" value maybeComma
        write 0 "enum class %s%s {" name widthSuffix
        emitValues 1
        write 0 "};"
        if info.FlagsMode then
            write 0 "DECLARE_FLAGS(%s)" name
    | NamedBitsDef info ->
        let indexedGroups, widthType =
            let resolver resolvedName =
                resolvedNameToString context resolvedName TypeMode
            namedBitsIndexer info context.ModuleContext resolver name
        let maybeExtendsComment =
            match info.MaybeParent with
            | Some parent ->
                sprintf " // extends %s" (typeToCppString context BareType parent)
            | None ->
                ""
        let widthSuffix =
            match widthType with
            | Primitive (Int(true,Width32)) -> ""
            | _ -> sprintf " : %s" (typeToCppString context Default widthType)
        write 0 "enum class %s%s {%s" (upperFirst name) widthSuffix maybeExtendsComment
        for group, groupCommaStr in indexedGroups |> withTrailingCommas do
            match group.MaybeTitle with
            | Some title ->
                // bit group 'title' (inheritance source)
                write 1 "// %s" title
            | None ->
                ()
            for (index, bitName), commaStr in group.IndexedNames |> withTrailingCommas do
                let maybeComma =
                    if commaStr = "" then groupCommaStr else ","
                let oneSuffix =
                    if index > 31 then "LL" else ""
                write 1 "%s = 1%s << %d%s" bitName oneSuffix index maybeComma
        write 0 "};"
        write 0 "DECLARE_FLAGS(%s)" name
    | SumTypeDef enumValues ->
        renderHeaderSumType context writer name enumValues

and renderImplementationHeaderGroup (context: CppRenderContext) (writer: IFormatWriter) =
    let write, blank = writer.Write, writer.Blank

    let group =
        context.GroupContext

    // emit all group opaque handles at once, to minimize ordering / mutual referencing problems
    // unfortunately we can't emit them at the very top of the module, because it seems you can't prefix the struct declaration with a namespace?
    // edit: (what would be wrong with enclosing namespace { ... } at the top of the file? TODO
    if not group.Opaques.IsEmpty then
        blank()
        for name, info in group.Opaques do
            let extendsComment =
                match info.MaybeParent with
                | Some parentType ->
                    sprintf " // extends %s" (typeToCppString context Default parentType)
                | None ->
                    ""
            write 0 "struct __%s; typedef __%s* %sRef;%s" name name name extendsComment

    if not group.FwdOpaques.IsEmpty then
        blank()
        for name, parts in group.FwdOpaques do
            write 0 "struct __%s; typedef __%s* %sRef; // non-circular alias for [%s]" name name name (joinNameParts "::" parts)

    // emit sumtype fwd decls, so they can be mutually dependent
    if not group.SumTypes.IsEmpty then
        blank()
        for name, _ in group.SumTypes do
            write 0 "namespace %s {" name
            write 1 "class Base;"
            write 0 "}"

    let renderNamedFunc (namedFunc: NamedFunction) =
        let throwsComment =
            if namedFunc.Throws.IsEmpty then
                ""
            else
                let joined =
                    namedFunc.Throws
                    |> List.map (typeToCppString context Default)
                    |> String.concat ", "
                sprintf " // throws %s" joined
        let maybeStaticPrefix =
            if context.InsideInterfaceOrStructStatic then
                "static "
            else
                ""
        write 0 "%s%s %s(%s);%s" maybeStaticPrefix (typeToCppString context Default namedFunc.Returns) namedFunc.Name (joinedNamedFuncArgs context true namedFunc.Args) throwsComment

    for def in context.GroupContext.Defs do
        match def with
        | Global(globalName, globalType) ->
            let storage =
                if context.InsideInterfaceOrStructStatic then
                    "static"
                else
                    "extern"
            write 0 "%s const %s %s;" storage (typeToCppString context Default globalType) globalName
        | FunctionDef namedFunc ->
            renderNamedFunc namedFunc
        | ExtMethodsBlock info ->
            write 0 "// extension methods for: %s" (typeToCppString context Default info.Target)
            for method in synthesizeExtensionMethods context info do
                renderNamedFunc method
        | UserType(name, userType) ->
            renderHeaderUserType context writer name userType
        | Group(groupName, _) ->
            let renderContent () =
                let nextContext =
                    context.NavigateToSub(groupName)
                renderImplementationHeaderGroup nextContext (writer.WithExtraIndent 1)
            if context.InsideInterfaceOrStructStatic then
                write 0 "class %s {" groupName
                write 0 "public:"
                renderContent()
                write 0 "};"
            else
                write 0 "namespace %s {" groupName
                renderContent()
                write 0 "}"

// didn't end up needing this, but it's maybe a nice example for future template nonsense, if we ever need it
// though I'd like to eschew that kind of complexity if possible ...

// let optInNamedBits (context: CppRenderContext) (writer: IFormatWriter) =
//     let write, blank = writer.Write, writer.Blank
//     let moduleNameParts =
//         match context.ModuleContext.Module.Name with
//         | ModuleName parts -> parts
//     let rec recInner (context: CppRenderContext) =
//         let group =
//             context.GroupContext
//         for def in group.Defs do
//             match def with
//             | UserType (userTypeName, type_) ->
//                 let staticDefsDescend (maybeDefs: ModuleDefinition list option) =
//                     match maybeDefs with
//                     | Some _ ->
//                         let nextContext =
//                             context.NavigateToSub userTypeName
//                         recInner nextContext
//                     | None ->
//                         ()
//                 match type_ with
//                 | NamedBitsDef _ ->
//                     // need the fully qualified module + groups path of the named bits
//                     let qualifiedName =
//                         moduleNameParts @ group.Path @ [userTypeName]
//                         |> String.concat "::"
//                     blank()
//                     write 0 "template<>"
//                     write 0 "inline constexpr bool EnableBitMaskOperators<%s> = true;" qualifiedName
//                 | OpaqueDef info ->
//                     staticDefsDescend info.MaybeStaticDefs
//                 | StructDef info ->
//                     staticDefsDescend info.MaybeStaticDefs
//                 | InterfaceDef info ->
//                     staticDefsDescend info.MaybeStaticDefs
//                 | _ ->
//                     // ignore
//                     ()
//             | Group (groupName, _) ->
//                 let nextContext =
//                     context.NavigateToSub(groupName)
//                 recInner nextContext
//             | _ ->
//                 // irrelevant, ignore
//                 ()
//     recInner context

let generateImplementationH (context: CppRenderContext) (writer: IFormatWriter) (currentDir: string list) = // (maybeModulePrefix: string option) =
    let write, blank = writer.Write, writer.Blank
    let m =
        context.ModuleContext.Module
    write 0 "#pragma once"
    blank ()
    write 0 "#include \"%s.h\"" (moduleNameToPathNoExt (Some m.Name) (ModuleName ["support"; "NativeImplServer"]))
    write 0 "#include <functional>"
    write 0 "#include <memory>"
    write 0 "#include <string>"
    write 0 "#include <vector>"
    write 0 "#include <map>"
    write 0 "#include <tuple>"
    write 0 "#include <set>"
    write 0 "#include <optional>"
    write 0 "#include <expected>"
    write 0 "#include \"%s.hpp\"" (moduleNameToPathNoExt (Some m.Name) (ModuleName ["support"; "flags"]))

    // imports
    if not m.Imports.IsEmpty then
        blank()
        for importName in m.Imports do
            let paths =
                getModulePaths currentDir importName
            write 0 "#include \"%s\"" paths.ModuleHeader
            // match importName with
            // | ModuleName nameParts ->
            //     write 0 "using namespace ::%s;" (joinNameParts "::" nameParts)
    blank()
    match m.Name with
    | ModuleName nameParts ->
        write 0 "namespace %s" (joinNameParts "::" nameParts)
    write 0 "{"

    // set a mode that forces all imports to be fully qualified
    // long story short we were having collision issues in certain files (eg Library.cpp)
    // but it might have been a more general problem simply not encountered until now
    let context =
        { context with NameMode = ImplementationHeader }

    // // recursively emit (some) fwd declarations at top of module, to minimize ordering/dependency issues (currently for sumtypes, maybe for interfaces in the future)
    // // ... didn't seem possible to also do opaques here, couldn't prefix their definitions with namespaces?
    // let rec renderFwdDeclarations (group: GroupContext) (writer: IFormatWriter) =
    //     let write = writer.Write
    //     let nsPrefix =
    //         // no silly path clamping shenanigans here!
    //         if not group.Path.IsEmpty then
    //             (String.concat "::" group.Path) + "::"
    //         else
    //             ""
    //     if not group.SumTypes.IsEmpty then
    //         blank()
    //         for name, _ in group.SumTypes do
    //             write 0 "class %s%s::Base;" nsPrefix name
    //     for subName in group.SubOrder do
    //         let sub =
    //             group.SubGroups[subName]
    //         renderFwdDeclarations sub writer
    // renderFwdDeclarations context.GroupContext (writer.WithExtraIndent 1)

    renderImplementationHeaderGroup context (writer.WithExtraIndent 1)
    write 0 "}"

    // // some C++ template stuff has to occur in global namespace, so we handle that here
    // blank()
    // write 0 "// C++ template voodoo"
    // writer.EatBlank()
    // optInNamedBits context writer

let rec renderImplementationCppGroup (context: CppRenderContext) (writer: IFormatWriter) =
    let write, blank = writer.Write, writer.Blank
    let group =
        context.GroupContext

    // if the path is clamped (because we can't use "namespace X {" to descend into class or struct namespaces in the .cpp file)
    // then compute what the relative namespace prefix has to be, for emitting globals and function names
    let nsPrefix =
        clampedNsPrefix context

    let renderFunc (namedFunc: NamedFunction) =
        let throwsComment =
            if namedFunc.Throws.IsEmpty then
                ""
            else
                let joined =
                    namedFunc.Throws
                    |> List.map (typeToCppString context Default)
                    |> String.concat ", "
                sprintf " // throws %s" joined
        write 0 "%s %s%s(%s) {%s" (typeToCppString context Default namedFunc.Returns) nsPrefix namedFunc.Name (joinedNamedFuncArgs context false namedFunc.Args) throwsComment
        if namedFunc.Returns <> UnitType then
            write 1 "return %s;" (defaultValueForType context namedFunc.Returns)
        write 0 "}"

    for def in group.Defs do
        match def with
        | Global(globalName, globalType) ->
            write 0 "const %s %s%s = %s;" (typeToCppString context Default globalType) nsPrefix globalName (defaultValueForType context globalType)
        | FunctionDef namedFunc ->
            renderFunc namedFunc
        | ExtMethodsBlock info ->
            blank()
            write 0 "// ext methods block for: %s ====" (typeToCppString context Default info.Target)
            for method in synthesizeExtensionMethods context info do
                renderFunc method
            write 0 "// =============================="
        | UserType(name, userType) ->
            match userType with
            | OpaqueDef info ->
                write 0 "struct %s__%s {" nsPrefix (upperFirst name)
                write 1 "// your content here"
                write 1 "// (or delete this and cast your own opaque ptr to '%sRef' in the stubs below)" (upperFirst name)
                write 0 "};"
                match info.MaybeStaticDefs with
                | Some _ ->
                    let nextContext =
                        context.NavigateToSub(name)
                    if not context.InsideInterfaceOrStructStatic then
                        write 0 "namespace %s {" (upperFirst name)
                        renderImplementationCppGroup nextContext (writer.WithExtraIndent 1)
                        write 0 "}"
                    else
                        renderImplementationCppGroup nextContext writer
                | None ->
                    ()
                for namedFunc in synthesizeOpaqueMethods context name info do
                    renderFunc namedFunc
                    blank()
                blank()
            | InterfaceDef info ->
                match info.MaybeStaticDefs with
                | Some _ ->
                    let nextContext =
                        context.NavigateToSub(name)
                    renderImplementationCppGroup nextContext writer
                | None ->
                    ()
            | StructDef info ->
                match info.MaybeStaticDefs with
                | Some _ ->
                    let nextContext =
                        context.NavigateToSub(name)
                    renderImplementationCppGroup nextContext writer
                | None ->
                    ()
            | _ ->
                // write 0 "// userType: %s" name
                ()
        | Group(groupName, _) ->
            let nextContext =
                context.NavigateToSub(groupName)
            if not context.InsideInterfaceOrStructStatic then
                write 0 "namespace %s {" groupName
                renderImplementationCppGroup nextContext (writer.WithExtraIndent 1)
                write 0 "}"
            else
                renderImplementationCppGroup nextContext writer

let generateImplementationCpp (context: CppRenderContext) (writer: IFormatWriter) = // (className: string) =
    let write, blank = writer.Write, writer.Blank
    let m =
        context.ModuleContext.Module

    write 0 "#include \"generated/%s.h\"" m.Name.LastPart
    blank()

    // the only place it's safe to 'use' namespaces:
    for import in m.Imports do
        match import with
        | ModuleName nameParts ->
            write 0 "using namespace ::%s;" (joinNameParts "::" nameParts)
    blank()

    match m.Name with
    | ModuleName nameParts ->
        write 0 "namespace %s" (joinNameParts "::" nameParts)
    write 0 "{"
    renderImplementationCppGroup context (writer.WithExtraIndent 1)
    write 0 "}"


let projectDir (outParams: OutParams) =
    sprintf "%s/server/cpp/%sImpl" outParams.Path outParams.LibraryName

let writeLibrary (allModules: Module list) (writer: IFormatWriter) (currentDir: string list) = // (maybeModulePrefix: string option) =
    let write, blank = writer.Write, writer.Blank

    for m in allModules do
        let paths =
            getModulePaths currentDir m.Name
        write 0 "#include \"%s\"" paths.ModuleWrappersHeader // (moduleNameToPathNoExt None m.Name) // (relativeIncludeFilenameNoExt None m.Name)

    blank()
    write 0 "extern \"C\" int nativeLibraryInit() {"
    for m in allModules do
        match m.Name with
        | ModuleName nameParts ->
            write 1 "::%s::__register();" (joinNameParts "::" nameParts)
    write 1 "// should we do module inits here as well?"
    write 1 "// currently they are manually done on the C# side inside the <module>.Init methods (which perform registration first) - and those are individually called by Library.Init, which first calls nativeImplInit"
    write 1 "return 0;"
    write 0 "}"
    blank()
    write 0 "extern \"C\" void nativeLibraryShutdown() {"
    write 1 "// module shutdowns? see note above"
    write 0 "}"

let generateModule (moduleContext: ModuleContext) (outParams: OutParams) =
    let m =
        moduleContext.Module
    let paths =
        getModulePaths [] m.Name

    let baseNamespace, maybeModulePrefix, className =
        namespaceParts outParams m.Name
    let moduleDir =
        let subPath =
            maybeModulePrefix
            |> Option.map (fun mp -> "/" + mp.Replace(".", "/"))
            |> Option.defaultValue ""
        sprintf "%s%s" (projectDir outParams) subPath
    Directory.CreateDirectory(moduleDir) |> ignore

    let generatedDir =
        $"{moduleDir}/generated"
    Directory.CreateDirectory(generatedDir) |> ignore

    let filenameNoExt =
        // for .cpp implementation
        sprintf "%s/%s" moduleDir className

    let generatedFilenameNoExt =
        // for wrappers/.h
        sprintf "%s/%s" generatedDir className

    let modulePrefixString =
        maybeModulePrefix
        |> Option.map (fun mp -> $"{mp}.")
        |> Option.defaultValue ""
    printfn "C++ writing (%s.%s%s)" baseNamespace modulePrefixString className

    let wrappersFilenameBase =
        $"{generatedFilenameNoExt}_wrappers"
    let implementationHeaderFilename =
        $"{generatedFilenameNoExt}.h"
    let implementationFilename =
        // in the future we may auto-populate .cpp.new from the existing .cpp,
        //   but will never overwrite something the user has worked on
        $"{filenameNoExt}.cpp"
        |> safeFilename ".new"

    let context =
        { RenderContext = createRenderContext moduleContext
          MaybePathClamp = None
          NameMode = Normal }

    let generatedDirParts =
        paths.GeneratedDir.Split("/")
        |> Array.toList

    let replaceIfDifferent (tempFilename: string) (targetFilename: string) =
        if File.Exists targetFilename then
            let tempText =
                File.ReadAllText tempFilename
            let targetText =
                File.ReadAllText targetFilename
            if tempText <> targetText then
                File.Move(tempFilename, targetFilename, true)
        else
            // doesn't even exist yet
            File.Move(tempFilename, targetFilename)

    printfn "  - %s.h/.cpp" wrappersFilenameBase
    let wrappersTargetH, wrappersTempH =
        wrappersFilenameBase + ".h", Path.GetTempFileName()
    let wrappersTargetCpp, wrappersTempCpp =
        wrappersFilenameBase + ".cpp", Path.GetTempFileName()
    let writeWrappers() =
        // nested scope to force file closure after generateWrappers
        use wrappersOutputH =
            let writer = new StreamWriter(wrappersTempH)
            new SimpleFormatWriter(writer, 0)
        use wrappersOutputCpp =
            let writer = new StreamWriter(wrappersTempCpp)
            new SimpleFormatWriter(writer, 0)
        generateWrappers context wrappersOutputH wrappersOutputCpp generatedDirParts
    writeWrappers()
    replaceIfDifferent wrappersTempH wrappersTargetH
    replaceIfDifferent wrappersTempCpp wrappersTargetCpp

    printfn "  - %s" implementationHeaderFilename
    let implHeaderTarget, implHeaderTemp =
        implementationHeaderFilename, Path.GetTempFileName()
    let writeImplHeader() =
        // nested scope to force file closure
        use implHeaderOutput =
            let writer = new StreamWriter(implHeaderTemp)
            new SimpleFormatWriter(writer, 0)
        generateImplementationH context implHeaderOutput generatedDirParts
    writeImplHeader()
    replaceIfDifferent implHeaderTemp implHeaderTarget

    printfn "  - %s" implementationFilename
    use implementationOutput =
        let writer = new StreamWriter(implementationFilename)
        new SimpleFormatWriter(writer, 0)
    generateImplementationCpp context implementationOutput // className

let generateLibrary (allModules: Module list) (outParams: OutParams) =
    let baseNamespace, maybeModulePrefix, className =
        namespaceParts outParams (ModuleName ["Library"])
    let moduleDir =
        let subPath =
            maybeModulePrefix
            |> Option.map (fun mp -> "/" + mp.Replace(".", "/"))
            |> Option.defaultValue ""
        sprintf "%s%s" (projectDir outParams) subPath
    Directory.CreateDirectory(moduleDir) |> ignore

    let generatedDir =
        $"{moduleDir}/generated"
    Directory.CreateDirectory(generatedDir) |> ignore

    let generatedFilenameNoExt =
        // for wrappers/.h
        sprintf "%s/%s" generatedDir className

    let modulePrefixString =
        maybeModulePrefix
        |> Option.map (fun mp -> $"{mp}.")
        |> Option.defaultValue ""
    printfn "C++ writing (%s.%s%s)" baseNamespace modulePrefixString className

    let filename =
        $"{generatedFilenameNoExt}.cpp"
    use libraryWriter =
        let writer = new StreamWriter(filename)
        new SimpleFormatWriter(writer, 0)
    writeLibrary allModules libraryWriter ["generated"]

let writeProject (writer: IFormatWriter) (allModules: Module list) (outParams: OutParams) =
    let write, blank = writer.Write, writer.Blank
    write 0 "cmake_minimum_required(VERSION 4.2)"
    write 0 "project(%sImpl)" outParams.LibraryName
    blank()
    write 0 "set(CMAKE_CXX_STANDARD 23)"
    blank()
    write 0 "add_library(%sImpl STATIC)" outParams.LibraryName
    blank()
    write 0 "target_sources(%sImpl" outParams.LibraryName
    write 2 "PRIVATE"
    write 2 "# support"
    write 2 "support/CoreStuff.h"
    write 2 "support/NativeImplServer.h"
    write 2 "support/NativeImplServer.cpp"
    write 2 "support/result.h"
    blank()
    for m in allModules do
        let paths =
            getModulePaths [] m.Name
        write 2 "# module %s" (joinModuleName PascalCase m.Name)
        write 2 "%s" paths.ModuleHeader
        write 2 "%s" paths.ModuleWrappersHeader
        write 2 "%s" paths.ModuleWrappersCpp
        write 2 "%s" paths.ModuleCpp
    blank()
    write 2 "# library implementation"
    write 2 "generated/Library.cpp"
    write 0 ")"

let generateProject (allModules: Module list) (outParams: OutParams) =
    let filename =
        sprintf "%s/CMakeLists.txt" (projectDir outParams)
        |> safeFilename ".new"
    use output =
        let writer = new StreamWriter(filename)
        new SimpleFormatWriter(writer, 0)
    writeProject output allModules outParams

let copySupportFiles (outParams: OutParams) =
    let destRoot =
        sprintf "%s/support" (projectDir outParams)
    SupportCopier.copyFiles "server/cpp" destRoot true ["*.h"; "*.cpp"; "*.hpp"] true None
