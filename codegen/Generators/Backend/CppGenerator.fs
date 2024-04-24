module Generators.Backend.CppGenerator

open System.IO
open AppCommon
open Parser

open Util
open Generators.Common
open Extensions

type CppRenderContext = {
    CommonContext: RenderContext
    EnablePathClamping: bool  // use when rendering .cpp files, because they can't use namespace { } to traverse down into class namespaces. used when rendering names, to add the explicit prefix (eg class1::subclass::thing)
} with
    member this.ModuleContext = this.CommonContext.ModuleContext
    member this.GroupContext = this.CommonContext.GroupContext
    member this.NavigateToSub(name: string) =
        let nextCommon =
            let nextGroup =
                this.CommonContext.GroupContext.SubGroups[name]
            { this.CommonContext with GroupContext = nextGroup }
        { this with CommonContext = nextCommon }

let resolvedNameToString (context: CppRenderContext) =
    let maybePathClamp =
        if context.EnablePathClamping then
            let prefix =
                // we want only those segments, from the start, who aren't part of a class namespace
                List.zip context.GroupContext.Path context.GroupContext.KindPath
                |> List.takeWhile (fun (_, kind) -> kind = NormalOrOpaqueStatic)
                |> List.map fst
            Some prefix
        else
            None
    resolvedNameToStringAbstract "::" maybePathClamp context.CommonContext
    
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

let typeToCppString (context: CppRenderContext) (outerType: NIType) =
    let rec recInner = function
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
            | StringType -> "std::string"
        | Resolved (resolvedName, resolvedType) ->
            let name =
                resolvedNameToString context resolvedName TypeMode
            match resolvedType with
            | OpaqueType ->
                sprintf "%sRef" name
            | InterfaceType ->
                sprintf "std::shared_ptr<%s>" name
            | DelegateType ->
                sprintf "std::function<%s>" name
            | EnumType ->
                let definition =
                    requiredSymbolLookup context.ModuleContext resolvedName
                match definition with
                | EnumDef enumInfo ->
                    if enumInfo.FlagsMode then
                        let widthType =
                            enumInfo.MaybeWidth
                            |> Option.defaultValue (Primitive(Int(false, Width32)))
                        recInner widthType
                    else
                        name
                | _ -> failwith "typeToCppString - problem fetching enum definition"
            | _ ->
                name
        | ArrayType niType -> sprintf "std::vector<%s>" (recInner niType)
        | MapType (keyType, valueType) -> sprintf "std::map<%s,%s>" (recInner keyType) (recInner valueType)
        | TupleType tupleTypes -> sprintf "std::tuple<%s>" (tupleTypes |> List.map recInner |> String.concat ",")
        | SetType niType -> sprintf "std::set<%s>" (recInner niType)
        | OptionType elem -> sprintf "std::optional<%s>" (recInner elem)
        | ResultType (succ, err) -> sprintf "result::Result<%s, %s>" (recInner succ) (recInner err)
        | BufferType elemType -> sprintf "std::shared_ptr<NativeBuffer<%s>>" (recInner elemType)
        | x -> sprintf "(typeToCppString: not done yet [%A])" x
    recInner outerType

let joinedFields (context: CppRenderContext) (fields: NamedField list) =
    [for field in fields -> sprintf "%s %s" (typeToCppString context field.Type_) field.Name]
    |> String.concat ", "

let joinedFieldNames (fields: NamedField list) =
    [for field in fields -> field.Name]
    |> String.concat ", "

let joinedArgs (context: CppRenderContext) (args: (string * NIType) list) =
    [for name, type_ in args -> sprintf "%s %s" (typeToCppString context type_) name]
    |> String.concat ", "

let joinedArgNames (args: (string * NIType) list) =
    args
    |> List.map fst
    |> String.concat ", "
    
let prefixLastNameSegment (name: string) (prefix: string) =
    let x =
        name.Split("::")
        |> Array.toList
        |> List.rev
    match x with
    | last :: etc ->
        (prefix + last) :: etc
    | _ -> failwith "bleh"
    |> List.rev
    |> String.concat "::"

let defaultValueForType (context: CppRenderContext) (outerType: NIType) =
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
            | OpaqueType ->
                // the final segment actually needs __ in front of it, since the opaque struct defs are defined with that
                // silly kludge for now, because AFAIK we don't need to directly deal with the default opaque structs (prefixed with __) anywhere else in the generation
                let replaced =
                    prefixLastNameSegment name "__"
                sprintf "new %s()" replaced
            | InterfaceType ->
                sprintf "std::shared_ptr<%s>()" name
            | DelegateType ->
                sprintf "std::function<%s>()" name
            | StructType ->
                sprintf "%s{}" name
            | _ ->
                "(defaultValueForType: not yet supported)"
        | ArrayType niType -> sprintf "std::vector<%s>()" (typeToCppString context niType)
        | MapType (keyType, valueType) -> sprintf "std::map<%s,%s>()" (typeToCppString context keyType) (typeToCppString context valueType)
        | TupleType tupleTypes ->
            let typesJoined =
                tupleTypes |> List.map (typeToCppString context) |> String.concat ","
            let defaultsJoined =
                tupleTypes |> List.map recInner |> String.concat ", "
            sprintf "std::tuple<%s>{ %s }" typesJoined defaultsJoined
        | SetType niType -> sprintf "std::set<%s>()" (typeToCppString context niType)
        | BufferType elemType -> sprintf "std::shared_ptr<NativeBuffer<%s>>()" (typeToCppString context elemType)
        | OptionType elem -> sprintf "std::optional<%s>()" (typeToCppString context elem)
        | ResultType (succ, _) -> sprintf "result::Ok(%s)" (recInner succ)
        | _ ->
            "(defaultValueForType: not yet supported)"
    recInner outerType

type PopValueContext =
    | Variable of name: string
    | LeftAssign of leftSide: string
    | Return
    | SafeReturn
    | ExceptionContext

let popValue (context: CppRenderContext) (writer: IFormatWriter) (level: int) (type_: NIType) (popContext: PopValueContext) =
    let write = writer.Write
    let contextPrefix =
        match popContext with
        | Variable varName -> sprintf "auto %s =" varName
        | LeftAssign leftSide -> sprintf "%s = " leftSide
        | Return -> "return"
        | SafeReturn -> "auto __ret ="
        | ExceptionContext -> ""
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
    | _ ->
        ()

type PushIsReturn =
    | Direct of value: bool
    | Indirect of name: string
    | NotApplicable

let pushValue (context: CppRenderContext) (writer: IFormatWriter) (level: int) (type_: NIType) (content: string) (isReturn: PushIsReturn) =
    let write = writer.Write
    let isReturnString() =
        match isReturn with
        | Direct value -> if value then "true" else "false"
        | Indirect name -> name
        | NotApplicable -> failwith "pushValue: 'NotApplicable' isReturn was consumed"
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
        | StructType | InterfaceType | TypedefType | SumType | OptionsType ->
            write level "%s__push(%s, %s);" name content (isReturnString())
        | _ ->
            write level "%s__push(%s);" name content
    | ArrayType niType ->
        match niType with
        | Primitive prim ->
            match prim with
            | SizeT -> write level "pushSizeTArrayInternal(%s);" content
            | Bool -> write level "pushBoolArrayInternal(%s);" content
            | Int(signed, intWidth) ->
                write level "push%sInt%dArrayInternal(%s);" (if signed then "" else "U") (widthToInt intWidth) content
            | Float32 -> write level "pushFloatArrayInternal(%s);" content
            | Float64 -> write level "pushDoubleArrayInternal(%s);" content
            | StringType -> write level "pushStringArrayInternal(%s);" content
        | ArrayType _ ->
            write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
        | MapType (keyType, valueType) ->
            write level "__%s%sMapArray__push(%s, %s);" (typeToName context keyType) (typeToName context valueType) content (isReturnString())
        | TupleType _ ->
            write level "__%sArray__push(%s, %s);" (typeToName context niType) content (isReturnString())
        | SetType _ ->
            write level "__%sArray__push(%s, %s);" (typeToName context niType) content (isReturnString())
        | OptionType _ ->
            write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
        | ResultType _ ->
            write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
        | Resolved _ ->
            write level "__%s__push(%s, %s);" (typeToName context (ArrayType niType)) content (isReturnString())
        | _ ->
            write level "// (push not yet supported: ArrayType %A)" niType
    | MapType (keyType, valueType) ->
        write level "__%s_%s_Map__push(%s, %s);" (typeToName context keyType) (typeToName context valueType) content (isReturnString())
    | TupleType _ ->
        write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
    | SetType _ ->
        write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
    | OptionType _ ->
        write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
    | ResultType _ ->
        write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
    | BufferType _ ->
        write level "__%s__push(%s, %s);" (typeToName context type_) content (isReturnString())
    | _ ->
        write level "// (pushValue not supported yet: %A)" type_

let renderWrapperDelegate (context: CppRenderContext) (writerH: IFormatWriter) (writerCpp: IFormatWriter) (name: string) (info: DelegateInfo) =
    let writeCpp, blankCpp = writerCpp.Write, writerCpp.Blank
    let writeH, _ = writerH.Write, writerH.Blank
    // push
    writeH 0 "void %s__push(std::function<%s> f);" name name
    writeCpp 0 "void %s__push(std::function<%s> f) {" name name
    writeCpp 1 "size_t uniqueKey = 0;"
    writeCpp 1 "if (f) {"
    writeCpp 2 "%s* ptr_fun = f.target<%s>();" name name
    writeCpp 2 "if (ptr_fun != nullptr) {"
    writeCpp 3 "uniqueKey = (size_t)ptr_fun;"
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
                let exceptionName =
                    resolvedNameToString context resolvedName IdentifierMode
                writeCpp 2 "catch (const %s& e) {" (typeToCppString context exc)
                writeCpp 3 "ni_setException(%s);" (lowerFirst exceptionName)
                pushValue context writerCpp 3 exc "e" (Direct true)
                writeCpp 2 "}"
            | _ ->
                failwithf "renderWrapperDelegate: 'throws' contained non exception type (%A)" exc
    writeCpp 1 "};"
    writeCpp 1 "pushServerFuncVal(wrapper, uniqueKey);"
    writeCpp 0 "}"
    // pop =============
    writeH 0 "std::function<%s> %s__pop();" name name
    blankCpp()
    writeCpp 0 "std::function<%s> %s__pop() {" name name
    writeCpp 1 "auto id = ni_popClientFunc();"
    writeCpp 1 "auto cf = std::shared_ptr<ClientFuncVal>(new ClientFuncVal(id));"
    writeCpp 1 "auto wrapper = [cf](%s) {" (joinedArgs context info.Args)
    for argName, argType in info.Args |> List.rev do
        pushValue context writerCpp 2 argType argName (Direct false)
    if info.Throws.IsEmpty then
        writeCpp 2 "cf->remoteExec();"
    else
        writeCpp 2 "cf->remoteExecWithExceptions();"
    if containsRefs context.ModuleContext info.Returns then
        popValue context writerCpp 2 info.Returns SafeReturn
    else
        popValue context writerCpp 2 info.Returns Return
    writeCpp 1 "};"
    writeCpp 1 "return wrapper;"
    writeCpp 0 "}"

let renderWrapperInterface (context: CppRenderContext) (writerH: IFormatWriter) (writerCpp: IFormatWriter) (outerType: NIType) (name: string) (methods: NamedFunction list) = // (typeContainsRefs: NIType -> bool) =
    let writeCpp, blankCpp = writerCpp.Write, writerCpp.Blank
    let writeH, blankH = writerH.Write, writerH.Blank
    
    // interface-to-pushable map
    // this allows users to use bare interfaces, instead of having to inherit from something that implements Pushable
    // a bit indirect but makes development that much more obvious and direct,
    // which is the point of NativeImpl!
    writeCpp 0 "static std::map<%s*, std::weak_ptr<Pushable>> __%sToPushable;" name (lowerFirst name)
    
    // server interface wrapper (holds a server interface ptr and implements Pushable)
    blankCpp()
    writeCpp 0 "class Server%sWrapper : public ServerObject {" name
    writeCpp 0 "public:"
    writeCpp 1 "std::shared_ptr<%s> rawInterface;" name
    writeCpp 0 "private:"
    writeCpp 1 "Server%sWrapper(std::shared_ptr<%s> raw) {" name name
    writeCpp 2 "this->rawInterface = raw;"
    writeCpp 1 "}"
    writeCpp 1 "void releaseExtra() override {"
    // remove entry from map, remote side released us
    writeCpp 2 "__%sToPushable.erase(rawInterface.get());" (lowerFirst name)
    writeCpp 1 "}"
    writeCpp 0 "public:"
    writeCpp 1 "static std::shared_ptr<Server%sWrapper> wrapAndRegister(std::shared_ptr<%s> raw) {" name name
    writeCpp 2 "auto ret = std::shared_ptr<Server%sWrapper>(new Server%sWrapper(raw));" name name
    writeCpp 2 "__%sToPushable[raw.get()] = ret;" (lowerFirst name)
    writeCpp 2 "return ret;"
    writeCpp 1 "}"
    writeCpp 0 "};"
    
    // client wrapper
    writeCpp 0 "class Client%s : public ClientObject, public %s {" name name
    writeCpp 0 "public:"
    writeCpp 1 "Client%s(int id) : ClientObject(id) {}" name
    writeCpp 1 "~Client%s() override {" name
    writeCpp 2 "__%sToPushable.erase(this);" (lowerFirst name)
    writeCpp 1 "}"
    // methods
    for namedFunc in methods do
        writeCpp 1 "%s %s(%s) override {" (typeToCppString context namedFunc.Returns) namedFunc.Name (joinedArgs context namedFunc.Args)
        // push args (reverse order)
        for argName, argType in namedFunc.Args |> List.rev do
            pushValue context writerCpp 2 argType argName (Direct false)
        // invoke
        if namedFunc.Throws.IsEmpty then
            writeCpp 2 "invokeMethod(%s_%s);" (lowerFirst name) namedFunc.Name
        else
            writeCpp 2 "invokeMethodWithExceptions(%s_%s);" (lowerFirst name) namedFunc.Name
        // pop result
        if containsRefs context.ModuleContext namedFunc.Returns then
            // any sort of reference that might get freed if it was only a return value from the other side (potentially stored nowhere else, so protected by the safety area)
            // this is to prevent premature deref which could free the instance before we've gotten ahold of it
            popValue context writerCpp 2 namedFunc.Returns SafeReturn
        else
            popValue context writerCpp 2 namedFunc.Returns Return
        writeCpp 1 "}"
    // end
    writeCpp 0 "};"
    
    // PUSH ===================================
    blankH()
    writeH 0 "void %s__push(%s inst, bool isReturn);" (typeToName context outerType) (typeToCppString context outerType)
    blankCpp()
    writeCpp 0 "void %s__push(%s inst, bool isReturn) {" (typeToName context outerType) (typeToCppString context outerType)
    writeCpp 1 "if (inst != nullptr) {"
    writeCpp 2 "auto found = __%sToPushable.find(inst.get());" (lowerFirst name)
    writeCpp 2 "if (found != __%sToPushable.end()) {" (lowerFirst name)
    // if it's already in the table (client thing, or as-yet-unknown server thing),
    // retrieve and push directly
    writeCpp 3 "auto pushable = found->second.lock();"
    writeCpp 3 "pushable->push(pushable, isReturn);"
    writeCpp 2 "}"
    writeCpp 2 "else {"
    // if it's not in the table, it's a new server thing, needs to be wrapped and pushed
    writeCpp 3 "auto pushable = Server%sWrapper::wrapAndRegister(inst);" name
    writeCpp 3 "pushable->push(pushable, isReturn);"
    writeCpp 2 "}"
    writeCpp 1 "}"
    writeCpp 1 "else {"
    writeCpp 2 "ni_pushNull();"
    writeCpp 1 "}"
    writeCpp 0 "}"
    
    // POP ====================================
    writeH 0 "%s %s__pop();" (typeToCppString context outerType) (typeToName context outerType)
    blankCpp()
    writeCpp 0 "%s %s__pop() {" (typeToCppString context outerType) (typeToName context outerType)
    writeCpp 1 "bool isClientID;"
    writeCpp 1 "auto id = ni_popInstance(&isClientID);"
    writeCpp 1 "if (id != 0) {"
    writeCpp 2 "if (isClientID) {"
    writeCpp 3 "auto ret = std::shared_ptr<%s>(new Client%s(id));" name name
    // store incoming client things so we can look them up by interface
    writeCpp 3 "__%sToPushable[ret.get()] = std::dynamic_pointer_cast<Pushable>(ret);" (lowerFirst name)
    writeCpp 3 "return ret;"
    writeCpp 2 "}"
    writeCpp 2 "else {"
    // getting back something we previously sent over, so we know a wrapper already exists
    writeCpp 3 "auto wrapper = std::static_pointer_cast<Server%sWrapper>(ServerObject::getByID(id));" name
    writeCpp 3 "return wrapper->rawInterface;"
    writeCpp 2 "}"
    writeCpp 1 "}"
    writeCpp 1 "else {"
    writeCpp 2 "return %s();" (typeToCppString context outerType)
    writeCpp 1 "}"
    writeCpp 0 "}"
    
    // SERVER STUFF ============================
    for nf in methods do
        blankH()
        writeH 0 "void %s_%s__wrapper(int serverID);" name nf.Name
        blankCpp()
        writeCpp 0 "void %s_%s__wrapper(int serverID) {" name nf.Name
        writeCpp 1 "auto wrapper = std::static_pointer_cast<Server%sWrapper>(ServerObject::getByID(serverID));" name
        writeCpp 1 "auto inst = wrapper->rawInterface;"
        for argName, argType in nf.Args do
            popValue context writerCpp 1 argType (Variable argName)
        let invocation =
            sprintf "inst->%s(%s)" nf.Name (joinedArgNames nf.Args)
        if nf.Throws.IsEmpty then
            pushValue context writerCpp 1 nf.Returns invocation (Direct true)
        else
            writeCpp 1 "try {" 
            pushValue context writerCpp 2 nf.Returns invocation (Direct true)
            writeCpp 1 "}"
            for exc in nf.Throws do
                match exc with
                | Resolved (resolvedName, ExceptionType) ->
                    let typeName =
                        resolvedNameToString context resolvedName TypeMode
                    writeCpp 1 "catch (const %s& e) {" typeName
                    writeCpp 2 "ni_setException(%s);" (lowerFirst typeName)
                    writeCpp 2 "%s__push(e);" typeName
                    writeCpp 1 "}"
                | _ ->
                    failwithf "C++ interface wrappers: 'throws' contained non-exception type %A" exc
        writeCpp 0 "}"
    

let renderWrapperSumType (context: CppRenderContext) (writerH: IFormatWriter) (writerCpp: IFormatWriter) (sumTypeName: string) (values: SumTypeValue list) =
    let writeCpp, blankCpp = writerCpp.Write, writerCpp.Blank
    let writeH, _ = writerH.Write, writerH.Blank
    let tagType =
        Primitive(Int(true,Width32))
    // push
    writeH 0 "void %s__push(%s value, bool isReturn);" sumTypeName sumTypeName
    writeCpp 0 "void %s__push(%s value, bool isReturn) {" sumTypeName sumTypeName
    writeCpp 1 "switch (value.tag) {"
    for value in values do
        writeCpp 1 "case %s::Tag::%s:" sumTypeName value.Name
        for argName, argType in value.Args |> List.rev do
            let content =
                sprintf "value.%s->%s" (lowerFirst value.Name) argName
            pushValue context writerCpp 2 argType content (Indirect "isReturn")
        writeCpp 2 "break;"
    writeCpp 1 "}"
    pushValue context writerCpp 1 tagType "(int32_t)value.tag" (Indirect "isReturn")
    writeCpp 0 "}"
    // pop
    writeH 0 "%s %s__pop();" sumTypeName sumTypeName
    blankCpp()
    writeCpp 0 "%s %s__pop() {" sumTypeName sumTypeName
    popValue context writerCpp 1 tagType (Variable "which")
    writeCpp 1 "switch ((%s::Tag)which) {" sumTypeName
    for value in values do
        writeCpp 1 "case %s::Tag::%s: {" sumTypeName value.Name
        for argName, argType in value.Args do
            popValue context writerCpp 2 argType (Variable argName)
        writeCpp 2 "return %s::%s::make(%s); }" sumTypeName value.Name (joinedArgNames value.Args)
    writeCpp 1 "default:"
    writeCpp 2 "throw \"%s__pop(): unknown tag!\";" sumTypeName
    writeCpp 1 "}"
    writeCpp 0 "}"

type ArrayOrSetRenderer(context: CppRenderContext, writer: IFormatWriter, outerType: NIType, elemType: NIType) =
    // TODO! this doesn't yet support .H file writing
    // but then do we ever really need that? aren't all generics contained within the wrapper .cpp file?
    // and rendered in strict dependency order? would the .H be required to avoid some kind of circularity?
    let write, blank = writer.Write, writer.Blank
    // use this for any types with no particular specializations (typedefs, delegates, arrays of arrays -- everything but structs and tuples, basically)
    let arrayDefault () =
        // PUSH ========
        write 0 "void __%s__push(%s values, bool isReturn) {" (typeToName context outerType) (typeToCppString context outerType)
        write 1 "for (auto v = values.rbegin(); v != values.rend(); v++) {" // in reverse (for arrays - for sets, order doesn't matter)
        pushValue context writer 2 elemType "*v" (Indirect "isReturn")
        write 1 "}"
        write 1 "ni_pushSizeT(values.size());"
        write 0 "}"
        // POP =========
        blank()
        write 0 "%s __%s__pop() {" (typeToCppString context outerType) (typeToName context outerType)
        write 1 "%s __ret;" (typeToCppString context outerType)
        write 1 "auto count = ni_popSizeT();"
        write 1 "for (auto i = 0; i < count; i++) {"
        popValue context writer 2 elemType (Variable "value")
        match outerType with
        | ArrayType _ ->
            write 2 "__ret.push_back(value);"
        | SetType _ ->
            write 2 "__ret.insert(value);"
        | _ ->
            failwith "!!!"
        write 1 "}"
        write 1 "return __ret;"
        write 0 "}"
    interface IArrayLikeRenderer with
        member this.TypedefTuple (tt: NIType) =
            // push
            write 0 "inline void __%s__push(%s values, bool isReturn) {" (typeToName context outerType) (typeToCppString context outerType)
            pushValue context writer 1 (ArrayType tt) "values" (Indirect "isReturn")
            write 0 "}"
            // pop
            blank()
            write 0 "inline %s __%s__pop() {" (typeToCppString context outerType) (typeToName context outerType)
            popValue context writer 1 (ArrayType tt) Return
            write 0 "}"
        member this.EnumType (name: ResolvedName) =
            let info =
                match requiredSymbolLookup context.ModuleContext name with
                | EnumDef info -> info
                | _ -> failwith "C++ ArrayOrSetRenderer: failed to look up enum definition"
            if info.FlagsMode then
                // TODO: figure out the right width type to use - for now just relying on uint32_t
                // PUSH ===================================
                write 0 "inline void __%s__push(%s values, bool isReturn) {" (typeToName context outerType) (typeToCppString context outerType)
                pushValue context writer 1 (ArrayType (Primitive(Int(false,Width32)))) "values" NotApplicable
                write 0 "}"
                // POP =====================================
                write 0 "inline %s __%s__pop() {" (typeToCppString context outerType) (typeToName context outerType)
                popValue context writer 1  (ArrayType (Primitive(Int(false,Width32)))) PopValueContext.Return
                write 0 "}"
            else
                let widthType =
                    // what int size will we use to encode these?
                    match info.MaybeWidth with
                    | Some provided ->
                        // use what the user asked for (currently no checking to verify it's big enough)
                        provided
                    | None ->
                        // what's the smallest width that will accommodate the enum size?
                        let iWidth =
                            match info.Values.Length with
                            | x when x < 128 -> Width8
                            | x when x < 32768 -> Width16
                            | _ -> Width32
                        Primitive(Int(true, iWidth))
                // PUSH ================
                write 0 "void __%s__push(%s values, bool isReturn) {" (typeToName context outerType) (typeToCppString context outerType)
                write 1 "std::vector<%s> intValues;" (typeToCppString context widthType)
                write 1 "for (auto i = values.begin(); i != values.end(); i++) {"
                write 2 "intValues.push_back((%s)*i);" (typeToCppString context widthType)
                write 1 "}"
                pushValue context writer 1 (ArrayType widthType) "intValues" (Indirect "isReturn")
                write 0 "}"
                // POP ==================
                blank()
                write 0 "%s __%s__pop() {" (typeToCppString context outerType) (typeToName context outerType)
                popValue context writer 1 (ArrayType widthType) (Variable "intValues")
                write 1 "%s __ret;" (typeToCppString context outerType)
                write 1 "for (auto i = intValues.begin(); i != intValues.end(); i++) {"
                let enumTypeStr =
                    resolvedNameToString context name TypeMode
                match outerType with
                | ArrayType _ ->
                    write 2 "__ret.push_back((%s)*i);" enumTypeStr
                | SetType _ ->
                    write 2 "__ret.insert((%s)*i);" enumTypeStr
                | _ ->
                    failwith "!!!"
                write 1 "}"
                write 1 "return __ret;"
                write 0 "}"
        member this.StructType (structName: ResolvedName) (namedFields: NamedField list) =
            let typeStr =
                resolvedNameToString context structName TypeMode
            // PUSH ===========
            write 0 "void __%s__push(%s values, bool isReturn) {" (typeToName context outerType) (typeToCppString context outerType)
            for field in namedFields |> List.rev do
                write 1 "std::vector<%s> %s_values;" (typeToCppString context field.Type_) field.Name
            write 1 "for (auto v = values.begin(); v != values.end(); v++) {"
            for field in namedFields |> List.rev do
                write 2 "%s_values.push_back(v->%s);" field.Name field.Name
            write 1 "}"
            for field in namedFields |> List.rev do
                pushValue context writer 1 (ArrayType field.Type_) (sprintf "%s_values" field.Name) (Indirect "isReturn")
            write 0 "}"
            // POP ============
            blank()
            write 0 "%s __%s__pop() {" (typeToCppString context outerType) (typeToName context outerType)
            for field in namedFields do
                popValue context writer 1 (ArrayType field.Type_) (Variable (sprintf "%s_values" field.Name))
            write 1 "%s __ret;" (typeToCppString context outerType)
            // use first field to iterate
            write 1 "for (auto i = 0; i < %s_values.size(); i++) {" namedFields.Head.Name
            write 2 "%s __value;" typeStr
            for field in namedFields do
                write 2 "__value.%s = %s_values[i];" field.Name field.Name
            match outerType with
            | ArrayType _ ->
                write 2 "__ret.push_back(__value);"
            | SetType _ ->
                write 2 "__ret.insert(__value);"
            | _ ->
                failwith "!!!"
            write 1 "}"
            write 1 "return __ret;"
            write 0 "}"
        member this.TupleType (tupleTypes: NIType list) =
            // PUSH ========
            write 0 "void __%s__push(%s values, bool isReturn) {" (typeToName context outerType) (typeToCppString context outerType)
            for i, type_ in tupleTypes |> List.indexed do
                write 1 "std::vector<%s> _%dvalues;" (typeToCppString context type_) i
            write 1 "for (auto i = values.begin(); i != values.end(); i++) {"
            for i, _ in tupleTypes |> List.indexed do
                write 2 "_%dvalues.push_back(std::get<%d>(*i));" i i
            write 1 "}"
            // push in reverse field order
            for i, type_ in tupleTypes |> List.indexed |> List.rev do
                pushValue context writer 1 (ArrayType type_) (sprintf "_%dvalues" i) (Indirect "isReturn")
            write 0 "}"
            // POP =========
            blank()
            write 0 "%s __%s__pop() {" (typeToCppString context outerType) (typeToName context outerType)
            for i, type_ in tupleTypes |> List.indexed do
                popValue context writer 1 (ArrayType type_) (Variable (sprintf "_%dvalues" i))
            write 1 "%s __ret;" (typeToCppString context outerType)
            write 1 "for (auto i = 0; i < _0values.size(); i++) {"
            let joined =
                tupleTypes |> List.indexed |> List.map fst |> List.map (sprintf "_%dvalues[i]") |> String.concat ", "
            match outerType with
            | ArrayType _ ->
                write 2 "__ret.push_back({ %s });" joined
            | SetType _ ->
                write 2 "__ret.insert({ %s });" joined
            | _ ->
                failwith "!!!"
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
            write 0 "void __%s__push(%s values, bool isReturn) {" (typeToName context outerType) (typeToCppString context outerType)
            write 1 "%s hasValueArray;" (typeToCppString context (ArrayType (Primitive Bool)))
            write 1 "hasValueArray.reserve(values.size());"
            write 1 "%s valueArray;" (typeToCppString context (ArrayType elemType))
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
            write 0 "%s __%s__pop() {" (typeToCppString context outerType) (typeToName context outerType)
            popValue context writer 1 (ArrayType (Primitive Bool)) (Variable "hasValueArray")
            popValue context writer 1 (ArrayType elemType) (Variable "valueArray")
            write 1 "%s ret(hasValueArray.size());" (typeToCppString context outerType) // allocate w/ default values = None
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
            write 0 "void __%s__push(%s values, bool isReturn) {" (typeToName context outerType) (typeToCppString context outerType)
            write 1 "%s isSuccessArray;" (typeToCppString context (ArrayType (Primitive Bool)))
            write 1 "isSuccessArray.reserve(values.size());"
            write 1 "%s successValuesArray;" (typeToCppString context (ArrayType succType))
            write 1 "%s errorValuesArray;" (typeToCppString context (ArrayType errType))
            write 1 "for (auto i = values.begin(); i != values.end(); i++) {"
            write 2 "if (i->is_ok()) {"
            write 3 "successValuesArray.push_back(i->unwrap());"
            write 3 "isSuccessArray.push_back(true);"
            write 2 "}"
            write 2 "else {"
            write 3 "errorValuesArray.push_back(i->unwrap_err());"
            write 3 "isSuccessArray.push_back(false);"
            write 2 "}"
            write 1 "}"
            pushValue context writer 1 (ArrayType errType) "errorValuesArray" (Indirect "isReturn")
            pushValue context writer 1 (ArrayType succType) "successValuesArray" (Indirect "isReturn")
            pushValue context writer 1 (ArrayType (Primitive Bool)) "isSuccessArray" NotApplicable
            write 0 "}"
            // pop ==========
            blank()
            write 0 "%s __%s__pop() {" (typeToCppString context outerType) (typeToName context outerType)
            popValue context writer 1 (ArrayType (Primitive Bool)) (Variable "isSuccessArray")
            popValue context writer 1 (ArrayType succType) (Variable "successValuesArray")
            popValue context writer 1 (ArrayType errType) (Variable "errorValuesArray")
            write 1 "%s ret;" (typeToCppString context outerType)
            write 1 "ret.reserve(isSuccessArray.size());"
            write 1 "auto succPtr = successValuesArray.data();"
            write 1 "auto errPtr = errorValuesArray.data();"
            write 1 "for (auto i = isSuccessArray.begin(); i != isSuccessArray.end(); i++) {"
            write 2 "if (*i) {"
            write 3 "ret.push_back(result::Ok(*succPtr++));"
            write 2 "}"
            write 2 "else {"
            write 3 "ret.push_back(result::Err(*errPtr++));"
            write 2 "}"
            write 1 "}"
            write 1 "return ret;"
            write 0 "}"
        member this.SumType () =
            arrayDefault()
        member this.Primitive (_: NIPrimitive) =
            // primitve arrays are built in, but not sets!
            match outerType with
            | ArrayType _ ->
                write 0 "// built-in array type: %s" (typeToCppString context outerType)
            | SetType niType ->
                // push ==============
                write 0 "void __%s__push(%s items, bool isReturn) {" (typeToName context outerType) (typeToCppString context outerType)
                write 1 "auto itemsArr = %s(items.begin(), items.end());" (typeToCppString context (ArrayType niType))
                pushValue context writer 1 (ArrayType niType) "itemsArr" (Indirect "isReturn")
                write 0 "}"
                // pop ===============
                blank()
                write 0 "%s __%s__pop() {" (typeToCppString context outerType) (typeToName context outerType)
                popValue context writer 1 (ArrayType niType) (Variable "itemsArr")
                write 1 "return %s(itemsArr.begin(), itemsArr.end());" (typeToCppString context outerType)
                write 0 "}"
            | _ -> failwith "!!!"
        member this.Opaque () =
            // push
            write 0 "void __%s__push(%s items, bool isReturn) {" (typeToName context outerType) (typeToCppString context outerType)
            write 1 "ni_pushPtrArray((void**)items.data(), items.size());"
            write 0 "}"
            // pop
            blank()
            write 0 "%s __%s__pop() {" (typeToCppString context outerType) (typeToName context outerType)
            write 1 "%s *values;" (typeToCppString context elemType)
            write 1 "size_t count;"
            write 1 "ni_popPtrArray((void***)&values, &count);"
            write 1 "return %s(values, values + count);" (typeToCppString context outerType)
            write 0 "}"
        member this.Options () =
            arrayDefault()
        member this.Other (_: NIType) =
            write 2 "// TODO: push/pop for array-like of %s" (typeToCppString context outerType)
            
let synthesizeOpaqueMethods2 (context: CppRenderContext) (opaqueName: string) (info: OpaqueInfo) =
    let fakeThis =
        let symbolName =
            SymbolName (context.GroupContext.Path @ [ opaqueName ])
        Resolved ((ThisModule symbolName), OpaqueType)
    let instanceMethods =
        let disposeMethod =
            { Name = "dispose"; Args = []; Returns = UnitType; Throws = [] }
        (info.Methods @ [ disposeMethod ]) // seems better at the end of instance methods
        // add "_this" parameter
        |> List.map (fun nf ->
            let nextArgs =
                let firstArg =
                    "_this", fakeThis
                firstArg :: nf.Args
            { nf with Args = nextArgs })
    // name prefix
    instanceMethods
    |> List.map (fun nf ->
        let nextName =
            sprintf "%s_%s" opaqueName nf.Name
        { nf with Name = nextName })
    
let renderWrapperNamedFunc (context: CppRenderContext) (writerH: IFormatWriter) (writerCpp: IFormatWriter) (namedFunction: NamedFunction) =
    let writeCpp, blankCpp = writerCpp.Write, writerCpp.Blank
    let writeH, blankH = writerH.Write, writerH.Blank
    blankH()
    writeH 0 "void %s__wrapper();" namedFunction.Name
    blankCpp()
    writeCpp 0 "void %s__wrapper() {" namedFunction.Name
    for argName, argType in namedFunction.Args do
        popValue context writerCpp 1 argType (Variable argName)
    let invocation =
        sprintf "%s(%s)" namedFunction.Name (joinedArgNames namedFunction.Args)
    if namedFunction.Throws.IsEmpty then
        pushValue context writerCpp 1 namedFunction.Returns invocation (Direct true)
    else
        writeCpp 1 "try {"
        pushValue context writerCpp 2 namedFunction.Returns invocation (Direct true)
        writeCpp 1 "}"
        for exc in namedFunction.Throws do
            match exc with
            | Resolved (resolvedName, ExceptionType) ->
                let typeName =
                    resolvedNameToString context resolvedName TypeMode
                writeCpp 1 "catch (const %s& e) {" typeName
                writeCpp 2 "ni_setException(%s);" (lowerFirst typeName)
                pushValue context writerCpp 2 exc "e" (Direct true)
                writeCpp 1 "}"
            | _ ->
                failwithf "C++ plain function wrappers: 'throws' contained non-exception type"
    writeCpp 0 "}"

let renderWrapperGeneric (context: CppRenderContext) (writerH: IFormatWriter) (writerCpp: IFormatWriter) (gType: NIType) =
    let writeCpp, blankCpp = writerCpp.Write, writerCpp.Blank
    let writeH, blankH = writerH.Write, writerH.Blank
    match gType with
    | BufferType elemType ->
        // PUSH =====
        blankH()
        writeH 0 "inline void __%s__push(%s buf, bool isReturn);" (typeToName context gType) (typeToCppString context gType)
        writeCpp 0 "inline void __%s__push(%s buf, bool isReturn) {" (typeToName context gType) (typeToCppString context gType)
        writeCpp 1 "if (buf != nullptr) {"
        writeCpp 2 "auto pushable = std::dynamic_pointer_cast<Pushable>(buf);"
        writeCpp 2 "pushable->push(pushable, isReturn);"
        writeCpp 1 "}"
        writeCpp 1 "else {"
        writeCpp 2 "ni_pushNull();"
        writeCpp 1 "}"
        writeCpp 0 "}"
        // POP ======
        writeH 0 "%s __%s__pop();" (typeToCppString context gType) (typeToName context gType)
        blankCpp()
        writeCpp 0 "%s __%s__pop() {" (typeToCppString context gType) (typeToName context gType)
        writeCpp 1 "int id;"
        writeCpp 1 "bool isClientId;"
        writeCpp 1 "ni_BufferDescriptor descriptor;"
        writeCpp 1 "ni_popBuffer(&id, &isClientId, &descriptor);"
        writeCpp 1 "if (id != 0) {"
        writeCpp 2 "if (isClientId) {"
        writeCpp 3 "auto buf = new ClientBuffer<%s>(id, descriptor);" (typeToCppString context elemType)
        writeCpp 3 "return %s(buf);" (typeToCppString context gType)
        writeCpp 2 "}"
        writeCpp 2 "else {"
        writeCpp 3 "return ServerBuffer<%s>::getByID(id);" (typeToCppString context elemType)
        writeCpp 2 "}"
        writeCpp 1 "}"
        writeCpp 1 "else {"
        writeCpp 2 "return %s();" (typeToCppString context gType)
        writeCpp 1 "}"
        writeCpp 0 "}"
    | TupleType tupleTypes ->
        // PUSH =====
        blankH()
        writeH 0 "void __%s__push(%s value, bool isReturn);" (typeToName context gType) (typeToCppString context gType)
        writeCpp 0 "void __%s__push(%s value, bool isReturn) {" (typeToName context gType) (typeToCppString context gType)
        for i, type_ in tupleTypes |> List.indexed |> List.rev do
            pushValue context writerCpp 1 type_ (sprintf "std::get<%d>(value)" i) (Indirect "isReturn")
        writeCpp 0 "}"
        // POP ======
        writeH 0 "%s __%s__pop();" (typeToCppString context gType) (typeToName context gType)
        blankCpp()
        writeCpp 0 "%s __%s__pop() {" (typeToCppString context gType) (typeToName context gType)
        for i, type_ in tupleTypes |> List.indexed do
            popValue context writerCpp 1 type_ (Variable (sprintf "_%d" i))
        let itemsJoined =
            tupleTypes
            |> List.indexed
            |> List.map (fun (i, _) -> sprintf "_%d" i)
            |> String.concat ", "
        writeCpp 1 "return %s{ %s };" (typeToCppString context gType) itemsJoined
        writeCpp 0 "}"
    | MapType (keyType, valueType) ->
        // PUSH =====
        blankH()
        writeH 0 "void __%s__push(%s _map, bool isReturn);" (typeToName context gType) (typeToCppString context gType)
        writeCpp 0 "void __%s__push(%s _map, bool isReturn) {" (typeToName context gType) (typeToCppString context gType)
        writeCpp 1 "std::vector<%s> keys;" (typeToCppString context keyType)
        writeCpp 1 "std::vector<%s> values;" (typeToCppString context valueType)
        writeCpp 1 "for (auto i = _map.begin(); i != _map.end(); i++) {"
        writeCpp 2 "keys.push_back(i->first);"
        writeCpp 2 "values.push_back(i->second);"
        writeCpp 1 "}"
        pushValue context writerCpp 1 (ArrayType valueType) "values" (Indirect "isReturn")
        pushValue context writerCpp 1 (ArrayType keyType) "keys" (Indirect "isReturn")
        writeCpp 0 "}"
        // POP =====
        writeH 0 "%s __%s__pop();" (typeToCppString context gType) (typeToName context gType)
        blankCpp()
        writeCpp 0 "%s __%s__pop() {" (typeToCppString context gType) (typeToName context gType)
        writeCpp 1 "%s __ret;" (typeToCppString context gType)
        popValue context writerCpp 1 (ArrayType keyType) (Variable "keys")
        popValue context writerCpp 1 (ArrayType valueType) (Variable "values")
        writeCpp 1 "for (auto i = 0; i < keys.size(); i++) {"
        writeCpp 2 "__ret[keys[i]] = values[i];"
        writeCpp 1 "}"
        writeCpp 1 "return __ret;"
        writeCpp 0 "}"
    | ArrayType niType ->
        let renderer =
            ArrayOrSetRenderer(context, writerCpp, gType, niType)
        renderArrayLike context.ModuleContext niType renderer
    | SetType niType ->
        let renderer =
            ArrayOrSetRenderer(context, writerCpp, gType, niType)
        renderArrayLike context.ModuleContext niType renderer
    | ResultType (succType, errType) ->
        // PUSH =====
        blankH()
        blankCpp()
        writeH 0 "void __%s__push(%s value, bool isReturn);" (typeToName context gType) (typeToCppString context gType)
        writeCpp 0 "void __%s__push(%s value, bool isReturn) {" (typeToName context gType) (typeToCppString context gType)
        writeCpp 1 "if (value.is_ok()) {"
        pushValue context writerCpp 2 succType "value.unwrap()" (Indirect "isReturn")
        pushValue context writerCpp 2 (Primitive Bool) "true" NotApplicable
        writeCpp 1 "}"
        writeCpp 1 "else {"
        pushValue context writerCpp 2 errType "value.unwrap_err()" (Indirect "isReturn")
        pushValue context writerCpp 2 (Primitive Bool) "false" NotApplicable
        writeCpp 1 "}"
        writeCpp 0 "}"
        // PUSH =====
        writeH 0 "%s __%s__pop();" (typeToCppString context gType) (typeToName context gType)
        blankCpp()
        writeCpp 0 "%s __%s__pop() {" (typeToCppString context gType) (typeToName context gType)
        popValue context writerCpp 1 (Primitive Bool) (Variable "isSuccess")
        writeCpp 1 "if (isSuccess) {"
        popValue context writerCpp 2 succType (Variable "successValue")
        writeCpp 2 "return result::Ok(successValue);"
        writeCpp 1 "}"
        popValue context writerCpp 1 errType (Variable "errValue")
        writeCpp 1 "return result::Err(errValue);"
        writeCpp 0 "}"
    | OptionType elem ->
        // PUSH =====
        blankH()
        blankCpp()
        writeH 0 "void __%s__push(%s value, bool isReturn);" (typeToName context gType) (typeToCppString context gType)
        writeCpp 0 "void __%s__push(%s value, bool isReturn) {" (typeToName context gType) (typeToCppString context gType)
        writeCpp 1 "if (value.has_value()) {"
        pushValue context writerCpp 2 elem "value.value()" (Indirect "isReturn")
        pushValue context writerCpp 2 (Primitive Bool) "true" NotApplicable
        writeCpp 1 "}"
        writeCpp 1 "else {"
        pushValue context writerCpp 2 (Primitive Bool) "false" NotApplicable
        writeCpp 1 "}"
        writeCpp 0 "}"
        // POP =====
        writeH 0 "%s __%s__pop();" (typeToCppString context gType) (typeToName context gType)
        blankCpp()
        writeCpp 0 "%s __%s__pop() {" (typeToCppString context gType) (typeToName context gType)
        writeCpp 1 "%s maybeValue;" (typeToCppString context gType)
        popValue context writerCpp 1 (Primitive Bool) (Variable "hasValue")
        writeCpp 1 "if (hasValue) {"
        popValue context writerCpp 2 elem (LeftAssign "maybeValue")
        writeCpp 1 "}"
        writeCpp 1 "return maybeValue;"
        writeCpp 0 "}"
    | _ ->
        writeCpp 0 "// push/pop for generic %s" (typeToCppString context gType)
    
    
let rec renderWrapperUserType (context: CppRenderContext) (writerH: IFormatWriter) (writerCpp: IFormatWriter) (name: string) (userType: UserType) =
    let writeCpp, blankCpp = writerCpp.Write, writerCpp.Blank
    let writeH, blankH = writerH.Write, writerH.Blank
    match userType with
    | OpaqueDef info ->
        blankH()
        writeH 0 "void %s__push(%sRef value);" name name
        writeCpp 0 "void %s__push(%sRef value) {" name name
        writeCpp 1 "ni_pushPtr(value);"
        writeCpp 0 "}"
        blankCpp()
        writeH 0 "%sRef %s__pop();" name name
        writeCpp 0 "%sRef %s__pop() {" name name
        writeCpp 1 "return (%sRef)ni_popPtr();" name
        writeCpp 0 "}"
        // nested static block
        match info.MaybeStaticDefs with
        | Some _ ->
            let nextContext =
                context.NavigateToSub(name)
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
        for nf in synthesizeOpaqueMethods2 context name info do
            renderWrapperNamedFunc context writerH writerCpp nf
    | StructDef namedFields ->
        blankH()
        writeH 0 "void %s__push(%s value, bool isReturn);" name name
        writeCpp 0 "void %s__push(%s value, bool isReturn) {" name name
        for field in namedFields |> List.rev do
            pushValue context writerCpp 1 field.Type_ (sprintf "value.%s" field.Name) (Indirect "isReturn")
        writeCpp 0 "}"
        blankCpp()
        writeH 0 "%s %s__pop();" name name
        writeCpp 0 "%s %s__pop() {" name name
        for field in namedFields do
            popValue context writerCpp 1 field.Type_ (Variable field.Name)
        writeCpp 1 "return %s { %s };" name (joinedFieldNames namedFields)
        writeCpp 0 "}"
    | OptionsDef namedFields ->
        let flagsType =
            Primitive(Int(true,Width32))
        // push
        blankH()
        writeH 0 "void %s__push(%s value, bool isReturn);" name name
        writeCpp 0 "void %s__push(%s value, bool isReturn) {" name name
        for field in namedFields |> List.rev do
            writeCpp 1 "%s %s;" (typeToCppString context field.Type_) field.Name
            writeCpp 1 "if (value.has%s(&%s)) {" (upperFirst field.Name) field.Name
            pushValue context writerCpp 2 field.Type_ field.Name (Indirect "isReturn")
            writeCpp 1 "}"
        pushValue context writerCpp 1 flagsType "value.getUsedFields()" NotApplicable
        writeCpp 0 "}"
        // pop
        blankCpp()
        writeH 0 "%s %s__pop();" name name
        writeCpp 0 "%s %s__pop() {" name name
        writeCpp 1 "%s value = {};" name
        popValue context writerCpp 1 flagsType (LeftAssign "value._usedFields")
        for field in namedFields do
            writeCpp 1 "if (value._usedFields & %s::Fields::%sField) {" name (upperFirst field.Name)
            popValue context writerCpp 2 field.Type_ (Variable "x")
            writeCpp 2 "value.set%s(x);" (upperFirst field.Name)
            writeCpp 1 "}"
        writeCpp 1 "return value;"
        writeCpp 0 "}"
    | ExceptionDef exFields ->
        blankH()
        writeH 0 "void %s__push(%s e);" name name
        blankCpp()
        writeCpp 0 "void %s__push(%s e) {" name name
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
        writeCpp 1 "throw %s(%s);" name (joinedFieldNames exFields)
        writeCpp 0 "}"
    | InterfaceDef info ->
        let outerType =
            let symbolName =
                SymbolName (context.GroupContext.Path @ [ name ])
            Resolved (ThisModule symbolName, InterfaceType)
        renderWrapperInterface context writerH writerCpp outerType name info.Methods
    | DelegateDef info ->
        renderWrapperDelegate context writerH writerCpp name info
    | TypeDef niType ->
        // ghetto, but for now instead of flattening, just write an inline wrapper
        blankH()
        writeH 0 "%s %s__pop();" name name
        writeCpp 0 "inline %s %s__pop() {" name name
        popValue context writerCpp 1 niType Return
        writeCpp 0 "}"
        writeH 0 "void %s__push(%s value, bool isReturn);" name name
        blankCpp()
        writeCpp 0 "inline void %s__push(%s value, bool isReturn) {" name name
        pushValue context writerCpp 1 niType "value" (Indirect "isReturn")
        writeCpp 0 "}"
    | EnumDef info ->
        let widthType =
            match info.MaybeWidth with
            | Some widthType -> widthType
            | None ->
                let signed =
                    not info.FlagsMode
                Primitive(Int(signed,Width32))
        if info.FlagsMode then
            blankH()
            writeH 0 "void %s__push(%s value);" name (typeToCppString context widthType)
            writeCpp 0 "void %s__push(%s value) {" name (typeToCppString context widthType)
            pushValue context writerCpp 1 widthType "value" NotApplicable
            writeCpp 0 "}"
            writeH 0 "%s %s__pop();" (typeToCppString context widthType) name
            blankCpp()
            writeCpp 0 "%s %s__pop() {" (typeToCppString context widthType) name
            popValue context writerCpp 1 widthType Return
            writeCpp 0 "}"
        else
            blankH()
            writeH 0 "void %s__push(%s value);" name name
            writeCpp 0 "void %s__push(%s value) {" name name
            let content =
                sprintf "(%s)value" (typeToCppString context widthType)
            pushValue context writerCpp 1 widthType content NotApplicable
            writeCpp 0 "}"
            writeH 0 "%s %s__pop();" name name
            blankCpp()
            writeCpp 0 "%s %s__pop() {" name name
            popValue context writerCpp 1 widthType (Variable "tag")
            writeCpp 1 "return (%s)tag;" name
            writeCpp 0 "}"
    | SumTypeDef values ->
        renderWrapperSumType context writerH writerCpp name values

and renderWrappersGroup (context: CppRenderContext) (writerH: IFormatWriter) (writerCpp: IFormatWriter) =
    let writeCpp, _ = writerCpp.Write, writerCpp.Blank
    let writeH, _ = writerH.Write, writerH.Blank
    let group =
        context.GroupContext
        
    // declare various module handles etc at top of group
    for name, info in group.Interfaces do
        for method in info.Methods do
            writeCpp 0 "ni_InterfaceMethodRef %s_%s;" (lowerFirst name) method.Name
            
    for name, _ in group.Exceptions do
        writeCpp 0 "ni_ExceptionRef %s;" (lowerFirst name)
        
    // go!
    for def in group.Defs do
        match def with
        | Global _ ->
            ()
        | FunctionDef namedFunction ->
            renderWrapperNamedFunc context writerH writerCpp namedFunction
        | UserType(name, userType) ->
            renderWrapperUserType context writerH writerCpp name userType
        | Group(groupName, _) ->
            let nextContext =
                context.NavigateToSub(groupName)
            writeH 0 "namespace %s" groupName
            writeCpp 0 "namespace %s" groupName
            writeH 0 "{"
            writeCpp 0 "{"
            renderWrappersGroup nextContext (writerH.WithExtraIndent 1) (writerCpp.WithExtraIndent 1)
            writeH 0 "}"
            writeCpp 0 "}"
        
let generateWrappers (context: CppRenderContext) (writerH: IFormatWriter) (writerCpp: IFormatWriter) (currentDir: string list) = // (maybeModulePrefix: string option) (className: string) =
    let writeCpp, blankCpp = writerCpp.Write, writerCpp.Blank
    let writeH, blankH = writerH.Write, writerH.Blank
    let m =
        context.ModuleContext.Module

    // begin output: ===============================================
    // header
    writeH 0 "#pragma once"
    writeH 0 "#include \"%s.h\"" m.Name.LastPart
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
        match importName with
        | ModuleName nameParts ->
            writeCpp 0 "using namespace %s;" (joinNameParts "::" nameParts)

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
        let rec groupCount (group: GroupContext) =
            group.Defs
            |> List.sumBy (function
                | Global _ -> 1
                | Group(name, _) ->
                   let sub =
                       group.SubGroups[name]
                   groupCount sub
                | _ -> 0)
        groupCount context.GroupContext
        
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
        // module methods (+ synthesized opaque)
        let opaqueMethods =
            group.Opaques
            |> List.collect (fun (name, info) -> synthesizeOpaqueMethods2 context name info)
        let allMethods =
            group.ModuleMethods @ opaqueMethods
        for namedFunction in allMethods do
            let parts =
                group.Path @ [ namedFunction.Name ]
            writeCpp 2 "ni_registerModuleMethod(m, \"%s\", &%s__wrapper);" (joinNameParts "." parts) (joinNameParts "::" parts)
        // interfaces + methods
        for name, info in group.Interfaces do
            let lowerIfaceName =
                lowerFirst name
            let parts =
                group.Path @ [ name ]
            let lowerParts =
                group.Path @ [ lowerIfaceName ]
            writeCpp 2 "auto %s = ni_registerInterface(m, \"%s\");" lowerIfaceName (joinNameParts "." parts)
            for method in info.Methods do
                let targetVarName =
                    sprintf "%s_%s" (joinNameParts "::" lowerParts) method.Name
                let wrapperName =
                    sprintf "%s_%s__wrapper" (joinNameParts "::" parts) method.Name
                writeCpp 2 "%s = ni_registerInterfaceMethod(%s, \"%s\", &%s);" targetVarName lowerIfaceName method.Name wrapperName
        // exceptions
        for name, _ in group.Exceptions do
            let parts =
                group.Path @ [ name ]
            let lowerName =
                lowerFirst name
            writeCpp 2 "%s = ni_registerException(m, \"%s\", &%s__buildAndThrow);" lowerName (joinNameParts "." parts) (joinNameParts "::" parts)
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
        write 1 "%s %s;" (typeToCppString context namedField.Type_) namedField.Name
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
    let write, _ = writer.Write, writer.Blank
    //
    write 0 "struct %s {" sumTypeName
    write 0 "public:"
    write 1 "enum class Tag {"
    for value, maybeComma in values |> withTrailingCommas do
        write 2 "%s%s" value.Name maybeComma
    write 1 "};"
    write 1 "const Tag tag;"
    for value in values do
        write 1 "struct %s {" value.Name
        for argName, argType in value.Args do
            write 2 "const %s %s;" (typeToCppString context argType) argName;
        write 2 "static %s make(%s) {" sumTypeName (joinedArgs context value.Args)
        write 3 "%s ret{ Tag::%s };" sumTypeName value.Name
        write 3 "ret.%s = std::shared_ptr<%s>(new %s{ %s });" (lowerFirst value.Name) value.Name value.Name (joinedArgNames value.Args)
        write 3 "return ret;"
        write 2 "}"
        write 1 "};"
    for value in values do
        write 1 "std::shared_ptr<%s> %s;" value.Name (lowerFirst value.Name)
    write 0 "};"
    
let renderHeaderOptionsType (context: CppRenderContext) (writer: IFormatWriter) (optionsTypeName: string) (namedFields: NamedField list) =
    let write, _ = writer.Write, writer.Blank
    //
    write 0 "struct %s {" optionsTypeName
    write 0 "private:"
    write 1 "enum Fields {"
    for (i, field), maybeComma in namedFields |> List.indexed |> withTrailingCommas do
        write 2 "%sField = %d%s" (upperFirst field.Name) (1 <<< i) maybeComma
    write 1 "};"
    let usedFieldsType =
        Primitive (Int(true, Width32))
    write 1 "%s _usedFields = %s;" (typeToCppString context usedFieldsType) (defaultValueForType context usedFieldsType)
    for field in namedFields do
        write 1 "%s _%s;" (typeToCppString context field.Type_) field.Name
    write 0 "protected:"
    write 1 "%s getUsedFields() {" (typeToCppString context usedFieldsType)
    write 2 "return _usedFields;"
    write 1 "}"
    write 1 "friend void %s__push(%s value, bool isReturn);" (upperFirst optionsTypeName) (upperFirst optionsTypeName)
    write 1 "friend %s %s__pop();" (upperFirst optionsTypeName) (upperFirst optionsTypeName)
    write 0 "public:"
    // accessor methods
    for field in namedFields do
        write 1 "void set%s(%s value) {" (upperFirst field.Name) (typeToCppString context field.Type_)
        write 2 "_%s = value;" field.Name
        write 2 "_usedFields |= Fields::%sField;" (upperFirst field.Name)
        write 1 "}"
        write 1 "bool has%s(%s *value) {" (upperFirst field.Name) (typeToCppString context field.Type_)
        write 2 "if (_usedFields & Fields::%sField) {" (upperFirst field.Name)
        write 3 "*value = _%s;" field.Name
        write 3 "return true;"
        write 2 "}"
        write 2 "return false;"
        write 1 "}"
    // done
    write 0 "};"
    
let rec renderHeaderUserType (context: CppRenderContext) (writer: IFormatWriter) (name: string) (userType: UserType) =
    let write, blank = writer.Write, writer.Blank
    //
    blank()
    match userType with
    | OpaqueDef info ->
        match info.MaybeStaticDefs with
        | Some _ ->
            let kind =
                context.GroupContext.Kind
            match kind with
            | NormalOrOpaqueStatic ->
                write 0 "namespace %s {" name
            | InterfaceStaticGroup ->
                write 0 "static class %s {" name
                write 0 "public:"
            let nextContext =
                context.NavigateToSub(name)
            renderImplementationHeaderGroup nextContext (writer.WithExtraIndent 1)
            // end namespace
            write 0 "}%s" (if kind = InterfaceStaticGroup then ";" else "")
        | None ->
            ()
        // methods
        let synthesized =
            synthesizeOpaqueMethods2 context name info
        for namedFunc in synthesized do
            let throwsComment =
                if namedFunc.Throws.IsEmpty then
                    ""
                else
                    let joined =
                        namedFunc.Throws
                        |> List.map (typeToCppString context)
                        |> String.concat ", "
                    sprintf " // throws %s" joined
            write 0 "%s %s(%s);%s" (typeToCppString context namedFunc.Returns) namedFunc.Name (joinedArgs context namedFunc.Args) throwsComment
    | StructDef namedFields ->
        write 0 "struct %s {" name
        for field in namedFields do
            write 1 "%s %s;" (typeToCppString context field.Type_) field.Name
        write 0 "};"
    | OptionsDef namedFields ->
        renderHeaderOptionsType context writer name namedFields
    | ExceptionDef namedFields ->
        renderHeaderException context writer name namedFields
    | InterfaceDef info ->
        let renderMethods (context: CppRenderContext) =
            write 1 "// instance methods ========================"
            for namedFunc in info.Methods do
                let throwsComment =
                    if namedFunc.Throws.IsEmpty then
                        ""
                    else
                        let joined =
                            namedFunc.Throws
                            |> List.map (typeToCppString context)
                            |> String.concat ", "
                        sprintf " // throws %s" joined
                write 1 "virtual %s %s(%s) = 0;%s" (typeToCppString context namedFunc.Returns) namedFunc.Name (joinedArgs context namedFunc.Args) throwsComment
        write 0 "class %s {" name
        write 0 "public:"
        match info.MaybeStaticDefs with
        | Some _ ->
            let nextContext =
                context.NavigateToSub(name)
            renderImplementationHeaderGroup nextContext (writer.WithExtraIndent 1)
            // render methods in context of static, so symbols are named correctly
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
                    |> List.map (typeToCppString context)
                    |> String.concat ", "
                sprintf " // throws %s" joined
        write 0 "typedef %s %s(%s);%s" (typeToCppString context info.Returns) name (joinedArgs context info.Args) maybeThrows
    | TypeDef actual ->
        write 0 "typedef %s %s;" (typeToCppString context actual) name
    | EnumDef info ->
        let maybeWidth =
            match info.MaybeWidth with
            | Some widthType ->
                sprintf " : %s" (typeToCppString context widthType)
            | None ->
                ""
        if info.FlagsMode then
            write 0 "enum %s%s {" name maybeWidth
        else
            write 0 "enum class %s%s {" name maybeWidth
        for (value, maybeSpecifiedValue), maybeComma in info.Values |> withTrailingCommas do
            match maybeSpecifiedValue with
            | Some specValue ->
                let enumValueStr =
                    match specValue with
                    | EnumValue.Decimal(value, leftShift) ->
                        if leftShift > 0 then
                            sprintf "%d << %d" value leftShift
                        else
                            sprintf "%d" value
                    | EnumValue.Hex(value, leftShift) ->
                        if leftShift > 0 then
                            sprintf "0x%X << %d" value leftShift
                        else
                            sprintf "0x%X" value
                write 1 "%s = %s%s" value enumValueStr maybeComma
            | None ->
                write 1 "%s%s" value maybeComma
        write 0 "};"
    | SumTypeDef enumValues ->
        renderHeaderSumType context writer name enumValues
    
and renderImplementationHeaderGroup (context: CppRenderContext) (writer: IFormatWriter) =
    let write, blank = writer.Write, writer.Blank
    
    let group =
        context.GroupContext
        
    if not group.Opaques.IsEmpty then
        blank()
        for name, info in group.Opaques do
            let extendsComment =
                match info.MaybeParent with
                | Some parentType ->
                    sprintf " // extends %s" (typeToCppString context parentType)
                | None ->
                    ""
            write 0 "struct __%s; typedef struct __%s* %sRef;%s" name name name extendsComment
    
    for def in context.GroupContext.Defs do
        match def with
        | Global(globalName, globalType) ->
            match group.Kind with
            | NormalOrOpaqueStatic ->
                write 0 "extern const %s %s;" (typeToCppString context globalType) globalName
            | InterfaceStaticGroup ->
                write 0 "static const %s %s;" (typeToCppString context globalType) globalName
        | FunctionDef namedFunc ->
            let throwsComment =
                if namedFunc.Throws.IsEmpty then
                    ""
                else
                    let joined =
                        namedFunc.Throws
                        |> List.map (typeToCppString context)
                        |> String.concat ", "
                    sprintf " // throws %s" joined
            match group.Kind with
            | NormalOrOpaqueStatic ->
                write 0 "%s %s(%s);%s" (typeToCppString context namedFunc.Returns) namedFunc.Name (joinedArgs context namedFunc.Args) throwsComment
            | InterfaceStaticGroup ->
                write 0 "static %s %s(%s);%s" (typeToCppString context namedFunc.Returns) namedFunc.Name (joinedArgs context namedFunc.Args) throwsComment
        | UserType(name, userType) ->
            renderHeaderUserType context writer name userType
        | Group(groupName, _) ->
            let nextContext =
                context.NavigateToSub(groupName)
            match group.Kind with
            | NormalOrOpaqueStatic ->
                write 0 "namespace %s {" groupName
            | InterfaceStaticGroup ->
                write 0 "static class %s {" groupName
                write 0 "public:"
            renderImplementationHeaderGroup nextContext (writer.WithExtraIndent 1)
            write 0 "}%s" (if group.Kind = InterfaceStaticGroup then ";" else "")
        
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
    write 0 "#include \"%s.h\"" (moduleNameToPathNoExt (Some m.Name) (ModuleName ["support"; "result"]))
    
    // imports
    if not m.Imports.IsEmpty then
        blank()
        for importName in m.Imports do
            let paths =
                getModulePaths currentDir importName
            write 0 "#include \"%s\"" paths.ModuleHeader
            match importName with
            | ModuleName nameParts ->
                write 0 "using namespace %s;" (joinNameParts "::" nameParts)
                // (moduleNameToPathNoExt (Some m.Name) importName) // (importName.Replace(".", "/"))
    blank()
    match m.Name with
    | ModuleName nameParts ->
        write 0 "namespace %s" (joinNameParts "::" nameParts)
    write 0 "{"

    // // recursively emit all opaque types at the top, so that there are no ordering/dependency issues
    // let rec renderOpaques (group: GroupContext) =
    //     if not group.Opaques.IsEmpty then
    //         blank()
    //         for name, info in group.Opaques do
    //             let extendsComment =
    //                 match info.MaybeParent with
    //                 | Some parentType ->
    //                     sprintf " // extends %s" (typeToCppString context parentType)
    //                 | None ->
    //                     ""
    //             let innerCommon =
    //                 sprintf "struct __%s; typedef struct __%s* %sRef;" name name name
    //             match group.Path with
    //             | [] ->
    //                 write 1 "%s%s" innerCommon extendsComment
    //             | _ ->
    //                 let nsJoined =
    //                     String.concat "::" group.Path
    //                 write 1 "namespace %s { %s }%s" nsJoined innerCommon extendsComment
    //             
    //     for subName in group.SubOrder do
    //         let sub = group.SubGroups[subName]
    //         renderOpaques sub
    // write 1 "// opaque fwd declarations"
    // renderOpaques context.GroupContext
    
    renderImplementationHeaderGroup context (writer.WithExtraIndent 1)
    write 0 "}"
    
let rec renderImplementationCppGroup (context: CppRenderContext) (writer: IFormatWriter) =
    let write, blank = writer.Write, writer.Blank
    let group =
        context.GroupContext
        
    let context =
        { context with EnablePathClamping = true } // because we can't use nested namespace {} to descend into class namespaces, we need to change how user types are rendered (with the explicit class prefix)
        
    let nsPrefix =
        match group.Kind with
        | NormalOrOpaqueStatic ->
            ""
        | InterfaceStaticGroup ->
            // since we can't use the usual nested 'namespace { ... }' with c++ classes,
            // we have to prefix classnames, sigh
            // find all non-namespace class segments since the last one and join them together
            List.zip group.Path group.KindPath
            |> List.filter (fun (_, kind) -> kind = InterfaceStaticGroup)
            |> List.map fst
            |> String.concat "::"
            |> sprintf "%s::"
        
    let renderFunc (namedFunc: NamedFunction) =
        let throwsComment =
            if namedFunc.Throws.IsEmpty then
                ""
            else
                let joined =
                    namedFunc.Throws
                    |> List.map (typeToCppString context)
                    |> String.concat ", "
                sprintf " // throws %s" joined
        write 0 "%s %s%s(%s) {%s" (typeToCppString context namedFunc.Returns) nsPrefix namedFunc.Name (joinedArgs context namedFunc.Args) throwsComment
        if namedFunc.Returns <> UnitType then
            write 1 "return %s;" (defaultValueForType context namedFunc.Returns)
        write 0 "}"
        
    for def in group.Defs do
        match def with
        | Global(globalName, globalType) ->
            write 0 "const %s %s%s = %s;" (typeToCppString context globalType) nsPrefix globalName (defaultValueForType context globalType)
        | FunctionDef namedFunc ->
            renderFunc namedFunc
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
                    match group.Kind with
                    | NormalOrOpaqueStatic ->
                        write 0 "namespace %s {" (upperFirst name)
                        renderImplementationCppGroup nextContext (writer.WithExtraIndent 1)
                        write 0 "}"
                    | InterfaceStaticGroup ->
                        renderImplementationCppGroup nextContext writer // no indent!
                | None ->
                    ()
                for namedFunc in synthesizeOpaqueMethods2 context name info do
                    renderFunc namedFunc
                blank()
            | InterfaceDef info ->
                match info.MaybeStaticDefs with
                | Some _ ->
                    let nextContext =
                        context.NavigateToSub(name)
                    renderImplementationCppGroup nextContext writer // no indent
                | None ->
                    ()
            | _ ->
                // write 0 "// userType: %s" name
                ()
        | Group(groupName, _) ->
            let nextContext =
                context.NavigateToSub(groupName)
            match group.Kind with
            | NormalOrOpaqueStatic ->
                write 0 "namespace %s {" groupName
                renderImplementationCppGroup nextContext (writer.WithExtraIndent 1)
                write 0 "}"
            | InterfaceStaticGroup ->
                renderImplementationCppGroup nextContext writer

let generateImplementationCpp (context: CppRenderContext) (writer: IFormatWriter) = // (className: string) =
    let write, blank = writer.Write, writer.Blank
    let m =
        context.ModuleContext.Module
        
    write 0 "#include \"generated/%s.h\"" m.Name.LastPart
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
            write 1 "%s::__register();" (joinNameParts "::" nameParts)
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
        $"{generatedFilenameNoExt}_Wrappers"
    let implementationHeaderFilename =
        $"{generatedFilenameNoExt}.h"
    let implementationFilename =
        // in the future we may auto-populate .cpp.new from the existing .cpp,
        //   but will never overwrite something the user has worked on
        let default' =
            $"{filenameNoExt}.cpp"
        if File.Exists(default') then
            $"{filenameNoExt}.cpp.new"
        else
            default'
            
    let context =
        { CommonContext = createRenderContext moduleContext
          EnablePathClamping = false }
        
    let generatedDirParts =
        paths.GeneratedDir.Split("/")
        |> Array.toList
        
    printfn "  - %s.h/.cpp" wrappersFilenameBase
    use wrappersOutputH =
        let writer = new StreamWriter(wrappersFilenameBase + ".h")
        new SimpleFormatWriter(writer, 0)
    use wrappersOutputCpp =
        let writer = new StreamWriter(wrappersFilenameBase + ".cpp")
        new SimpleFormatWriter(writer, 0)
    generateWrappers context wrappersOutputH wrappersOutputCpp generatedDirParts

    printfn "  - %s" implementationHeaderFilename
    use implHeaderOutput =
        let writer = new StreamWriter(implementationHeaderFilename)
        new SimpleFormatWriter(writer, 0)
    generateImplementationH context implHeaderOutput generatedDirParts
    
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
