module Generators.Backend.CppGenerator

open System.IO
open Parser

open Util
open Generators.Common
open Extensions

let rec typeToName = function
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
    | OpaqueHandleType name -> upperFirst name
    | InterfaceType name -> upperFirst name
    | ExceptionType name -> upperFirst name
    | DelegateType name -> upperFirst name
    | ArrayType niType -> sprintf "%sArray" (typeToName niType)
    | MapType (keyType, valueType) -> sprintf "%s%sMap" (typeToName keyType) (typeToName valueType)
    | TupleType tupleTypes -> sprintf "%sTuple" (tupleTypes |> List.map typeToName |> String.concat "")
    | SetType niType -> sprintf "%sSet" (typeToName niType)
    | BufferType elemType -> sprintf "Native%sBuffer" (typeToName elemType)
    | StructType name -> upperFirst name
    | TypedefType name -> upperFirst name
    | EnumType name -> upperFirst name
    | SumType name -> upperFirst name
    | OptionsType name -> upperFirst name
    | x -> sprintf "(typeToName: not done yet [%A]" x

let rec typeToCppString (typeMap: Map<string, UserOrGeneric>) = function
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
    | OpaqueHandleType name -> name
    | InterfaceType name -> sprintf "std::shared_ptr<%s>" name
    | ExceptionType name -> name
    | DelegateType name -> sprintf "std::function<%s>" name
    | ArrayType niType -> sprintf "std::vector<%s>" (typeToCppString typeMap niType)
    | MapType (keyType, valueType) -> sprintf "std::map<%s,%s>" (typeToCppString typeMap keyType) (typeToCppString typeMap valueType)
    | TupleType tupleTypes -> sprintf "std::tuple<%s>" (tupleTypes |> List.map (typeToCppString typeMap) |> String.concat ",")
    | SetType niType -> sprintf "std::set<%s>" (typeToCppString typeMap niType)
    | BufferType elemType -> sprintf "std::shared_ptr<NativeBuffer<%s>>" (typeToCppString typeMap elemType)
    | StructType name -> name
    | OptionsType name -> name
    | TypedefType name -> name
    | EnumType name ->
        match typeMap[name] with
        | UserOrGeneric.User (EnumDef enumInfo) ->
            if enumInfo.FlagsMode then
                let widthType =
                    enumInfo.MaybeWidth
                    |> Option.defaultValue (Primitive(Int(false, Width32)))
                typeToCppString typeMap widthType
            else
                name
        | _ ->
            failwithf "typeToCppString: failed to find enum type %s in type map" name
    | SumType name -> name
    | x -> sprintf "(typeToCppString: not done yet [%A])" x

let joinedFields (typeMap: Map<string, UserOrGeneric>) (fields: NamedField list) =
    [for field in fields -> sprintf "%s %s" (typeToCppString typeMap field.Type_) field.Name]
    |> String.concat ", "

let joinedFieldNames (fields: NamedField list) =
    [for field in fields -> field.Name]
    |> String.concat ", "

let joinedArgs (typeMap: Map<string, UserOrGeneric>) (args: (string * NIType) list) =
    [for name, type_ in args -> sprintf "%s %s" (typeToCppString typeMap type_) name]
    |> String.concat ", "

let joinedArgNames (args: (string * NIType) list) =
    args
    |> List.map fst
    |> String.concat ", "

let rec defaultValueForType (typeMap: Map<string, UserOrGeneric>) = function
    | UnitType -> failwith "defaultValueForType called on UnitType"
    | Primitive prim ->
        match prim with
        | SizeT -> "0"
        | Bool -> "false"
        | Int _ -> "0"
        | Float32 -> "0.0f"
        | Float64 -> "0.0"
        | StringType -> "\"default\""
    | OpaqueHandleType name -> sprintf "new __%s()" (upperFirst name)
    | InterfaceType name -> sprintf "std::shared_ptr<%s>()" name
    | DelegateType name -> sprintf "std::function<%s>()" name
    | ArrayType niType -> sprintf "std::vector<%s>()" (typeToCppString typeMap niType)
    | MapType (keyType, valueType) -> sprintf "std::map<%s,%s>()" (typeToCppString typeMap keyType) (typeToCppString typeMap valueType)
    | TupleType tupleTypes ->
        let typesJoined =
            tupleTypes |> List.map (typeToCppString typeMap) |> String.concat ","
        let defaultsJoined =
            tupleTypes |> List.map (defaultValueForType typeMap) |> String.concat ", "
        sprintf "std::tuple<%s>{ %s }" typesJoined defaultsJoined
    | SetType niType -> sprintf "std::set<%s>()" (typeToCppString typeMap niType)
    | BufferType elemType -> sprintf "std::shared_ptr<NativeBuffer<%s>>()" (typeToCppString typeMap elemType)
    | _ ->
        "(defaultValueForType: not yet supported)"

type PopValueContext =
    | Variable of name: string
    | LeftAssign of leftSide: string
    | Return
    | SafeReturn
    | ExceptionContext

let popValue (writer: IFormatWriter) (level: int) (type_: NIType) (context: PopValueContext) =
    let write = writer.Write
    let contextPrefix =
        match context with
        | Variable varName -> sprintf "auto %s =" varName
        | LeftAssign leftSide -> sprintf "%s = " leftSide
        | Return -> "return"
        | SafeReturn -> "auto __ret ="
        | ExceptionContext -> ""
    match type_ with
    | UnitType ->
        match context with
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
    | OpaqueHandleType name ->
        write level "%s (%s)ni_popPtr();" contextPrefix (upperFirst name)
    | InterfaceType ifaceName ->
        write level "%s %s__pop();" contextPrefix ifaceName
    | DelegateType delegateName ->
        write level "%s %s__pop();" contextPrefix delegateName
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
            | StructType name -> sprintf "__%sArray__pop" name
            | TypedefType name -> sprintf "__%sArray__pop" name
            | DelegateType name -> sprintf "__%sArray__pop" name
            | ArrayType _ -> sprintf "__%sArray__pop" (typeToName niType)
            | EnumType name -> sprintf "__%sArray__pop" name
            | SumType name -> sprintf "__%sArray__pop" name
            | TupleType _ -> sprintf "__%sArray__pop" (typeToName niType)
            | SetType _ -> sprintf "__%sArray__pop" (typeToName niType)
            | MapType _ -> sprintf "__%sArray__pop" (typeToName niType)
            | _ -> sprintf "// (pop not yet supported: %A)" type_
        write level "%s %s();" contextPrefix popFunc
    | MapType (keyType, valueType) ->
        write level "%s __%s%sMap__pop();" contextPrefix (typeToName keyType) (typeToName valueType)
    | TupleType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName type_)
    | SetType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName type_)
    | BufferType _ ->
        write level "%s __%s__pop();" contextPrefix (typeToName type_)
    | StructType structName ->
        write level "%s %s__pop();" contextPrefix structName
    | TypedefType typedefName ->
        write level "%s %s__pop();" contextPrefix typedefName
    | EnumType enumTypeName ->
        write level "%s %s__pop();" contextPrefix enumTypeName
    | SumType sumTypeName ->
        write level "%s %s__pop();" contextPrefix sumTypeName
    | OptionsType optionsTypeName ->
        write level "%s %s__pop();" contextPrefix optionsTypeName
    | ExceptionType exceptionName ->
        write level "%s__buildAndThrow();" exceptionName
    | _ ->
        write level "// (pop not yet implemented: %A)" type_
    // epilogue
    match context with
    | SafeReturn ->
        write level "ni_clearClientSafetyArea();"
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
    | OpaqueHandleType _ ->
        write level "ni_pushPtr(%s);" content
    | InterfaceType name ->
        write level "%s__push(%s, %s);" name content (isReturnString())
    | DelegateType name ->
        write level "%s__push(%s);" name content // still hmm about safety area for functions ...
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
        | ArrayType niType ->
            write level "__%sArrayArray__push(%s, %s);" (typeToName niType) content (isReturnString())
        | MapType (keyType, valueType) ->
            write level "__%s%sMapArray__push(%s, %s);" (typeToName keyType) (typeToName valueType) content (isReturnString())
        | TupleType _ ->
            write level "__%sArray__push(%s, %s);" (typeToName niType) content (isReturnString())
        | SetType _ ->
            write level "__%sArray__push(%s, %s);" (typeToName niType) content (isReturnString())
        | StructType name ->
            write level "__%sArray__push(%s, %s);" name content (isReturnString())
        | DelegateType name ->
            write level "__%sArray__push(%s, %s);" name content (isReturnString()) // currently doesn't use the isReturn' value, but lets us emit array stuff more easily
        | TypedefType name ->
            write level "__%sArray__push(%s, %s);" name content (isReturnString())
        | EnumType name ->
            write level "__%sArray__push(%s, %s);" name content (isReturnString())
        | SumType name ->
            write level "__%sArray__push(%s, %s);" name content (isReturnString())
        | _ ->
            write level "// (push not yet supported: ArrayType %A)" niType
    | MapType (keyType, valueType) ->
        write level "__%s%sMap__push(%s, %s);" (typeToName keyType) (typeToName valueType) content (isReturnString())
    | TupleType _ ->
        write level "__%s__push(%s, %s);" (typeToName type_) content (isReturnString())
    | SetType _ ->
        write level "__%s__push(%s, %s);" (typeToName type_) content (isReturnString())
    | BufferType _ ->
        write level "__%s__push(%s, %s);" (typeToName type_) content (isReturnString())
    | StructType name ->
        write level "%s__push(%s, %s);" name content (isReturnString())
    | TypedefType name ->
        write level "%s__push(%s, %s);" name content (isReturnString())
    | EnumType name ->
        write level "%s__push(%s);" name content
    | SumType name ->
        write level "%s__push(%s, %s);" name content (isReturnString())
    | OptionsType name ->
        write level "%s__push(%s, %s);" name content (isReturnString())
    | ExceptionType name ->
        assert (isReturn = Direct true)          // maybe needs a separate value for exceptions?
        write level "%s__push(%s);" name content // isreturn irrelevant, exceptions must push everything safely because they are basically returns
    | _ ->
        write level "// (pushValue not supported yet: %A)" type_


let renderWrapperDelegate (writer: IFormatWriter) (typeMap: Map<string, UserOrGeneric>) (name: string) (info: DelegateInfo) (typeContainsRefs: NIType -> bool) =
    let write, blank = writer.Write, writer.Blank
    // push
    write 0 "void %s__push(std::function<%s> f) {" name name
    write 1 "size_t uniqueKey = 0;"
    write 1 "if (f) {"
    write 2 "%s* ptr_fun = f.target<%s>();" name name
    write 2 "if (ptr_fun != nullptr) {"
    write 3 "uniqueKey = (size_t)ptr_fun;"
    write 2 "}"
    write 1 "}"
    write 1 "auto wrapper = [f]() {"
    for argName, argType in info.Args do
        popValue writer 2 argType (Variable argName)
    let invocation =
        sprintf "f(%s)" (joinedArgNames info.Args)
    if info.Throws.IsEmpty then
        pushValue writer 2 info.Returns invocation (Direct true)
    else
        write 2 "try {"
        pushValue writer 3 info.Returns invocation (Direct true)
        write 2 "}"
        for exc in info.Throws do
            match exc with
            | ExceptionType exceptionName ->
                write 2 "catch (const %s& e) {" (typeToCppString typeMap exc)
                write 3 "ni_setException(%s);" (lowerFirst exceptionName)
                pushValue writer 3 exc "e" (Direct true)
                write 2 "}"
            | _ ->
                failwithf "renderWrapperDelegate: 'throws' contained non exception type (%A)" exc
    write 1 "};"
    write 1 "pushServerFuncVal(wrapper, uniqueKey);"
    write 0 "}"
    // pop =============
    blank()
    write 0 "std::function<%s> %s__pop() {" name name
    write 1 "auto id = ni_popClientFunc();"
    write 1 "auto cf = std::shared_ptr<ClientFuncVal>(new ClientFuncVal(id));"
    write 1 "auto wrapper = [cf](%s) {" (joinedArgs typeMap info.Args)
    for argName, argType in info.Args |> List.rev do
        pushValue writer 2 argType argName (Direct false)
    if info.Throws.IsEmpty then
        write 2 "cf->remoteExec();"
    else
        write 2 "cf->remoteExecWithExceptions();"
    if typeContainsRefs info.Returns then
        popValue writer 2 info.Returns SafeReturn
    else
        popValue writer 2 info.Returns Return
    write 1 "};"
    write 1 "return wrapper;"
    write 0 "}"

let renderWrapperInterface (writer: IFormatWriter) (typeMap: Map<string, UserOrGeneric>) (name: string) (methods: NamedFunction list) (typeContainsRefs: NIType -> bool) =
    let write, blank = writer.Write, writer.Blank
    // client wrapper
    write 0 "class Client%s : public ClientObject, public %s {" name name
    write 0 "public:"
    write 1 "Client%s(int id) : ClientObject(id) {}" name
    // methods
    for namedFunc in methods do
        write 1 "%s %s(%s) override {" (typeToCppString typeMap namedFunc.Returns) namedFunc.Name (joinedArgs typeMap namedFunc.Args)
        // push args (reverse order)
        for argName, argType in namedFunc.Args |> List.rev do
            pushValue writer 2 argType argName (Direct false)
        // invoke
        if namedFunc.Throws.IsEmpty then
            write 2 "invokeMethod(%s_%s);" (lowerFirst name) namedFunc.Name
        else
            write 2 "invokeMethodWithExceptions(%s_%s);" (lowerFirst name) namedFunc.Name
        // pop result
        if typeContainsRefs namedFunc.Returns then
            // any sort of reference that might get freed if it was only a return value from the other side (potentially stored nowhere else, so protected by the safety area)
            // this is to prevent premature deref which could free the instance before we've gotten ahold of it
            popValue writer 2 namedFunc.Returns SafeReturn
        else
            popValue writer 2 namedFunc.Returns Return
        write 1 "}"
    // end
    write 0 "};"
    // PUSH ===================================
    let outerType =
        InterfaceType name
    blank()
    write 0 "void %s__push(%s inst, bool isReturn) {" (typeToName outerType) (typeToCppString typeMap outerType)
    write 1 "if (inst != nullptr) {"
    write 2 "auto pushable = std::dynamic_pointer_cast<Pushable>(inst);"
    write 2 "pushable->push(pushable, isReturn);"
    write 1 "}"
    write 1 "else {"
    write 2 "ni_pushNull();"
    write 1 "}"
    write 0 "}"
    // POP ====================================
    blank()
    write 0 "%s %s__pop()" (typeToCppString typeMap outerType) (typeToName outerType)
    write 0 "{"
    write 1 "bool isClientID;"
    write 1 "auto id = ni_popInstance(&isClientID);"
    write 1 "if (id != 0) {"
    write 2 "if (isClientID) {"
    write 3 "return %s(new Client%s(id));" (typeToCppString typeMap outerType) (typeToName outerType)
    write 2 "}"
    write 2 "else {"
    write 3 "return Server%s::getByID(id);" (typeToName outerType)
    write 2 "}"
    write 1 "}"
    write 1 "else {"
    write 2 "return %s();" (typeToCppString typeMap outerType)
    write 1 "}"
    write 0 "}"

let renderWrapperException (writer: IFormatWriter) (name: string) (exFields: NamedField list) =
    let write, blank = writer.Write, writer.Blank
    //
    blank()
    write 0 "void %s__push(%s e) {" name name
    // push field values (reverse order)
    for namedField in exFields |> List.rev do
        let memberVar =
            sprintf "e.%s" namedField.Name
        pushValue writer 1 namedField.Type_ memberVar (Direct true) // true = exceptions are always returned by definition, so any references in their fields need to be kept safe ... right?
    write 0 "}"
    // build and throw
    blank()
    write 0 "void %s__buildAndThrow() {" name
    for field in exFields do
        popValue writer 1 field.Type_ (Variable field.Name)
    // joined variables (_0, _1, etc)
    write 1 "throw %s(%s);" name (joinedFieldNames exFields)
    write 0 "}"

let renderWrapperSumType (writer: IFormatWriter) (sumTypeName: string) (values: SumTypeValue list) =
    let write, blank = writer.Write, writer.Blank
    let tagType =
        Primitive(Int(true,Width32))
    // push
    write 0 "void %s__push(%s value, bool isReturn) {" sumTypeName sumTypeName
    write 1 "switch (value.tag) {"
    for value in values do
        write 1 "case %s::Tag::%s:" sumTypeName value.Name
        for argName, argType in value.Args |> List.rev do
            let content =
                sprintf "value.%s->%s" (lowerFirst value.Name) argName
            pushValue writer 2 argType content (Indirect "isReturn")
        write 2 "break;"
    write 1 "}"
    pushValue writer 1 tagType "(int32_t)value.tag" (Indirect "isReturn")
    write 0 "}"
    // pop
    blank()
    write 0 "%s %s__pop() {" sumTypeName sumTypeName
    popValue writer 1 tagType (Variable "which")
    write 1 "switch ((%s::Tag)which) {" sumTypeName
    for value in values do
        write 1 "case %s::Tag::%s: {" sumTypeName (upperFirst value.Name)
        for argName, argType in value.Args do
            popValue writer 2 argType (Variable argName)
        write 2 "return %s::%s::make(%s); }" sumTypeName (upperFirst value.Name) (joinedArgNames value.Args)
    write 1 "default:"
    write 2 "throw \"%s__pop(): unknown tag!\";" sumTypeName
    write 1 "}"
    write 0 "}"

type ArrayOrSetRenderer(writer: IFormatWriter, typeMap: Map<string, UserOrGeneric>, outerType: NIType, elemType: NIType) =
    let write, blank = writer.Write, writer.Blank
    // use this for any types with no particular specializations (typedefs, delegates, arrays of arrays -- everything but structs and tuples, basically)
    let arrayDefault () =
        // PUSH ========
        write 0 "void __%s__push(%s values, bool isReturn) {" (typeToName outerType) (typeToCppString typeMap outerType)
        write 1 "for (auto v = values.rbegin(); v != values.rend(); v++) {" // in reverse (for arrays - for sets, order doesn't matter)
        pushValue writer 2 elemType "*v" (Indirect "isReturn")
        write 1 "}"
        write 1 "ni_pushSizeT(values.size());"
        write 0 "}"
        // POP =========
        blank()
        write 0 "%s __%s__pop() {" (typeToCppString typeMap outerType) (typeToName outerType)
        write 1 "%s __ret;" (typeToCppString typeMap outerType)
        write 1 "auto count = ni_popSizeT();"
        write 1 "for (auto i = 0; i < count; i++) {"
        popValue writer 2 elemType (Variable "value")
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
            write 0 "inline void __%s__push(%s values, bool isReturn) {" (typeToName outerType) (typeToCppString typeMap outerType)
            pushValue writer 1 (ArrayType tt) "values" (Indirect "isReturn")
            write 0 "}"
            // pop
            blank()
            write 0 "inline %s __%s__pop() {" (typeToCppString typeMap outerType) (typeToName outerType)
            popValue writer 1 (ArrayType tt) Return
            write 0 "}"
        member this.EnumType (name: string) =
            let widthType =
                match typeMap[name] with
                | UserOrGeneric.User (EnumDef info) ->
                    let signed =
                        not info.FlagsMode
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
                        Primitive(Int(signed, iWidth))
                | _ ->
                    failwith "C++ ArrayOrSetRenderer: weirdness in this.EnumType"
            // PUSH ================
            write 0 "void __%s__push(%s values, bool isReturn) {" (typeToName outerType) (typeToCppString typeMap outerType)
            write 1 "std::vector<%s> intValues;" (typeToCppString typeMap widthType)
            write 1 "for (auto i = values.begin(); i != values.end(); i++) {"
            write 2 "intValues.push_back((%s)*i);" (typeToCppString typeMap widthType)
            write 1 "}"
            pushValue writer 1 (ArrayType widthType) "intValues" (Indirect "isReturn")
            write 0 "}"
            // POP ==================
            blank()
            write 0 "%s __%s__pop() {" (typeToCppString typeMap outerType) (typeToName outerType)
            popValue writer 1 (ArrayType widthType) (Variable "intValues")
            write 1 "%s __ret;" (typeToCppString typeMap outerType)
            write 1 "for (auto i = intValues.begin(); i != intValues.end(); i++) {"
            match outerType with
            | ArrayType _ ->
                write 2 "__ret.push_back((%s)*i);" name
            | SetType _ ->
                write 2 "__ret.insert((%s)*i);" name
            | _ ->
                failwith "!!!"
            write 1 "}"
            write 1 "return __ret;"
            write 0 "}"
        member this.StructType (structName: string) (namedFields: NamedField list) =
            // PUSH ===========
            write 0 "void __%s__push(%s values, bool isReturn) {" (typeToName outerType) (typeToCppString typeMap outerType)
            for field in namedFields |> List.rev do
                write 1 "std::vector<%s> %s_values;" (typeToCppString typeMap field.Type_) field.Name
            write 1 "for (auto v = values.begin(); v != values.end(); v++) {"
            for field in namedFields |> List.rev do
                write 2 "%s_values.push_back(v->%s);" field.Name field.Name
            write 1 "}"
            for field in namedFields |> List.rev do
                pushValue writer 1 (ArrayType field.Type_) (sprintf "%s_values" field.Name) (Indirect "isReturn")
            write 0 "}"
            // POP ============
            blank()
            write 0 "%s __%s__pop() {" (typeToCppString typeMap outerType) (typeToName outerType)
            for field in namedFields do
                popValue writer 1 (ArrayType field.Type_) (Variable (sprintf "%s_values" field.Name))
            write 1 "%s __ret;" (typeToCppString typeMap outerType)
            // use first field to iterate
            write 1 "for (auto i = 0; i < %s_values.size(); i++) {" namedFields.Head.Name
            write 2 "%s __value;" structName
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
            write 0 "void __%s__push(%s values, bool isReturn) {" (typeToName outerType) (typeToCppString typeMap outerType)
            for i, type_ in tupleTypes |> List.indexed do
                write 1 "std::vector<%s> _%dvalues;" (typeToCppString typeMap type_) i
            write 1 "for (auto i = values.begin(); i != values.end(); i++) {"
            for i, _ in tupleTypes |> List.indexed do
                write 2 "_%dvalues.push_back(std::get<%d>(*i));" i i
            write 1 "}"
            // push in reverse field order
            for i, type_ in tupleTypes |> List.indexed |> List.rev do
                pushValue writer 1 (ArrayType type_) (sprintf "_%dvalues" i) (Indirect "isReturn")
            write 0 "}"
            // POP =========
            blank()
            write 0 "%s __%s__pop() {" (typeToCppString typeMap outerType) (typeToName outerType)
            for i, type_ in tupleTypes |> List.indexed do
                popValue writer 1 (ArrayType type_) (Variable (sprintf "_%dvalues" i))
            write 1 "%s __ret;" (typeToCppString typeMap outerType)
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
        member this.SumType () =
            arrayDefault()
        member this.Primitive (prim: NIPrimitive) =
            // primitve arrays are built in, but not sets!
            match outerType with
            | ArrayType _ ->
                write 0 "// built-in array type: %s" (typeToCppString typeMap outerType)
            | SetType niType ->
                // push ==============
                write 0 "void __%s__push(%s items, bool isReturn) {" (typeToName outerType) (typeToCppString typeMap outerType)
                write 1 "auto itemsArr = %s(items.begin(), items.end());" (typeToCppString typeMap (ArrayType niType))
                pushValue writer 1 (ArrayType niType) "itemsArr" (Indirect "isReturn")
                write 0 "}"
                // pop ===============
                blank()
                write 0 "%s __%s__pop() {" (typeToCppString typeMap outerType) (typeToName outerType)
                popValue writer 1 (ArrayType niType) (Variable "itemsArr")
                write 1 "return %s(itemsArr.begin(), itemsArr.end());" (typeToCppString typeMap outerType)
                write 0 "}"
            | _ -> failwith "!!!"
        member this.Other (t: NIType) =
            write 2 "// TODO: push/pop for array-like of %s" (typeToCppString typeMap outerType)

let renderWrapperUserTypes (writer: IFormatWriter) (containsRefsMap: Map<string,bool>) (name: string) (content: UserOrGeneric) (typeMap: Map<string, UserOrGeneric>) =
    let write, blank = writer.Write, writer.Blank
    //
    let typeContainsRefs (type_: NIType) =
        containsRefsMap.TryFind(typeToInternalKey type_)
        |> Option.defaultValue false
    blank()
    match content with
    | UserOrGeneric.FwdInterface ->
        write 0 "void %s__push(std::shared_ptr<%s> thing, bool isReturn);" name name
        write 0 "std::shared_ptr<%s> %s__pop();" name name
    | UserOrGeneric.Generic gType ->
        match gType with
        | BufferType elemType ->
            // PUSH =====
            write 0 "inline void __%s__push(%s buf, bool isReturn) {" (typeToName gType) (typeToCppString typeMap gType)
            write 1 "if (buf != nullptr) {"
            write 2 "auto pushable = std::dynamic_pointer_cast<Pushable>(buf);"
            write 2 "pushable->push(pushable, isReturn);"
            write 1 "}"
            write 1 "else {"
            write 2 "ni_pushNull();"
            write 1 "}"
            write 0 "}"
            // POP ======
            blank()
            write 0 "%s __%s__pop() {" (typeToCppString typeMap gType) (typeToName gType)
            write 1 "int id;"
            write 1 "bool isClientId;"
            write 1 "ni_BufferDescriptor descriptor;"
            write 1 "ni_popBuffer(&id, &isClientId, &descriptor);"
            write 1 "if (id != 0) {"
            write 2 "if (isClientId) {"
            write 3 "auto buf = new ClientBuffer<%s>(id, descriptor);" (typeToCppString typeMap elemType)
            write 3 "return %s(buf);" (typeToCppString typeMap gType)
            write 2 "}"
            write 2 "else {"
            write 3 "return ServerBuffer<%s>::getByID(id);" (typeToCppString typeMap elemType)
            write 2 "}"
            write 1 "}"
            write 1 "else {"
            write 2 "return %s();" (typeToCppString typeMap gType)
            write 1 "}"
            write 0 "}"
        | TupleType tupleTypes ->
            // PUSH =====
            write 0 "void __%s__push(%s value, bool isReturn) {" (typeToName gType) (typeToCppString typeMap gType)
            for i, type_ in tupleTypes |> List.indexed |> List.rev do
                pushValue writer 1 type_ (sprintf "std::get<%d>(value)" i) (Indirect "isReturn")
            write 0 "}"
            // POP ======
            blank()
            write 0 "%s __%s__pop() {" (typeToCppString typeMap gType) (typeToName gType)
            for i, type_ in tupleTypes |> List.indexed do
                popValue writer 1 type_ (Variable (sprintf "_%d" i))
            let itemsJoined =
                tupleTypes
                |> List.indexed
                |> List.map (fun (i, _) -> sprintf "_%d" i)
                |> String.concat ", "
            write 1 "return %s{ %s };" (typeToCppString typeMap gType) itemsJoined
            write 0 "}"
        | MapType (keyType, valueType) ->
            // PUSH =====
            write 0 "void __%s__push(%s _map, bool isReturn) {" (typeToName gType) (typeToCppString typeMap gType)
            write 1 "std::vector<%s> keys;" (typeToCppString typeMap keyType)
            write 1 "std::vector<%s> values;" (typeToCppString typeMap valueType)
            write 1 "for (auto i = _map.begin(); i != _map.end(); i++) {"
            write 2 "keys.push_back(i->first);"
            write 2 "values.push_back(i->second);"
            write 1 "}"
            pushValue writer 1 (ArrayType valueType) "values" (Indirect "isReturn")
            pushValue writer 1 (ArrayType keyType) "keys" (Indirect "isReturn")
            write 0 "}"
            // POP =====
            blank()
            write 0 "%s __%s__pop() {" (typeToCppString typeMap gType) (typeToName gType)
            write 1 "%s __ret;" (typeToCppString typeMap gType)
            popValue writer 1 (ArrayType keyType) (Variable "keys")
            popValue writer 1 (ArrayType valueType) (Variable "values")
            write 1 "for (auto i = 0; i < keys.size(); i++) {"
            write 2 "__ret[keys[i]] = values[i];"
            write 1 "}"
            write 1 "return __ret;"
            write 0 "}"
        | ArrayType niType ->
            let renderer =
                ArrayOrSetRenderer(writer, typeMap, gType, niType)
            renderArrayLike typeMap niType renderer
        | SetType niType ->
            let renderer =
                ArrayOrSetRenderer(writer, typeMap, gType, niType)
            renderArrayLike typeMap niType renderer
        | _ ->
            write 0 "// push/pop for generic %s" (typeToCppString typeMap gType)
    | UserOrGeneric.User userType ->
        match userType with
        | OpaqueHandleDef ->
            ()
        | StructDef namedFields ->
            write 0 "void %s__push(%s value, bool isReturn) {" name name
            for field in namedFields |> List.rev do
                pushValue writer 1 field.Type_ (sprintf "value.%s" field.Name) (Indirect "isReturn")
            write 0 "}"
            blank()
            write 0 "%s %s__pop() {" name name
            for field in namedFields do
                popValue writer 1 field.Type_ (Variable field.Name)
            write 1 "return %s { %s };" name (joinedFieldNames namedFields)
            write 0 "}"
        | OptionsDef namedFields ->
            let flagsType =
                Primitive(Int(true,Width32))
            // push
            write 0 "void %s__push(%s value, bool isReturn) {" name name
            for field in namedFields |> List.rev do
                write 1 "%s %s;" (typeToCppString typeMap field.Type_) field.Name
                write 1 "if (value.has%s(&%s)) {" (upperFirst field.Name) field.Name
                pushValue writer 2 field.Type_ field.Name (Indirect "isReturn")
                write 1 "}"
            pushValue writer 1 flagsType "value.getUsedFields()" NotApplicable
            write 0 "}"
            // pop
            blank()
            write 0 "%s %s__pop() {" name name
            write 1 "%s value = {};" name
            popValue writer 1 flagsType (LeftAssign "value._usedFields")
            for field in namedFields do
                write 1 "if (value._usedFields & %s::Fields::%s) {" name (upperFirst field.Name)
                popValue writer 2 field.Type_ (Variable "x")
                write 2 "value.set%s(x);" (upperFirst field.Name)
                write 1 "}"
            write 1 "return value;"
            write 0 "}"
        | ExceptionDef namedFields ->
            renderWrapperException writer name namedFields
        | InterfaceDef methods ->
            renderWrapperInterface writer typeMap name methods typeContainsRefs
        | DelegateDef info ->
            renderWrapperDelegate writer typeMap name info typeContainsRefs
        | TypeDef niType ->
            // ghetto, but for now instead of flattening, just write an inline wrapper
            write 0 "inline %s %s__pop() {" name name
            popValue writer 1 niType Return
            write 0 "}"
            blank()
            write 0 "inline void %s__push(%s value, bool isReturn) {" name name
            pushValue writer 1 niType "value" (Indirect "isReturn")
            write 0 "}"
        | EnumDef info ->
            let widthType =
                match info.MaybeWidth with
                | Some widthType -> widthType
                | None ->
                    let signed =
                        not info.FlagsMode
                    Primitive(Int(signed,Width32))
            if info.FlagsMode then
                write 0 "inline void %s__push(%s value) {" name (typeToCppString typeMap widthType)
                pushValue writer 1 widthType "value" NotApplicable
                write 0 "}"
                blank()
                write 0 "inline %s %s__pop() {" (typeToCppString typeMap widthType) name
                popValue writer 1 widthType Return
                write 0 "}"
            else
                write 0 "inline void %s__push(%s value) {" name name
                let content =
                    sprintf "(%s)value" (typeToCppString typeMap widthType)
                pushValue writer 1 widthType content NotApplicable
                write 0 "}"
                blank()
                write 0 "inline %s %s__pop() {" name name
                popValue writer 1 widthType (Variable "tag")
                write 1 "return (%s)tag;" name
                write 0 "}"
        | SumTypeDef values ->
            renderWrapperSumType writer name values

let getRelativePath (currentPath: string list) (targetPath: string list) =
    // remove common segments
    // prefix (currentPath.count) segments with ".."
    // then remainder of targetPath
    let current, target =
        (currentPath, targetPath)
        ||> List.skipWhile2 (fun a b -> a = b)
    List.replicate current.Length ".." @ target

let relativeIncludeFilename (maybeModulePrefix: string option) (targetModule: string) =
    // for including other module paths
    let targetParts =
        targetModule.Split(".")
        |> Array.map (fun part -> if part = "$PARENT" then ".." else part)
        |> List.ofArray
    match maybeModulePrefix with
    | Some prefix ->
            getRelativePath (prefix.Split(".") |> List.ofArray) targetParts
            |> String.concat "/"
    | None ->
        targetParts
        |> String.concat "/"
    |> sprintf "%s.h"

let generateWrappersCpp (m: Module) (writer: IFormatWriter) (maybeModulePrefix: string option) (className: string) =
    let write, blank = writer.Write, writer.Blank
    // things we'll need to use a few times:
    let globals =
        m.Defs
        |> List.choose (function
            | Global(name, niType) -> Some (name, niType)
            | _ -> None)
    let userTypes =
        m.Defs
        |> List.choose (function
            | UserType(name, userType) -> Some (name, userType)
            | _ -> None)
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
    // begin output: ===============================================
    // prologue
    write 0 "#include \"%s\"" (relativeIncludeFilename maybeModulePrefix "$PARENT.support.NativeImplServer")
    write 0 "#include \"%s.h\"" className

    // opaques + interface method + exception handles (for registration storage)
    blank()
    for name, userType in userTypes do
        let lowerName = lowerFirst name
        match userType with
        | OpaqueHandleDef ->
            write 0 "NIHANDLE(%s);" lowerName
        | InterfaceDef namedFunctions ->
            namedFunctions
            |> List.iter (fun nf ->
                write 0 "ni_InterfaceMethodRef %s_%s;" lowerName nf.Name)
        | ExceptionDef _ ->
            write 0 "ni_ExceptionRef %s;" lowerName
        | _ ->
            ()

    // user types and generics in dependency order
    let typeMap =
        userTypesAndGenerics m
    let containsRefsMap =
        createContainsRefsMap typeMap
    let renderFunc =
        renderWrapperUserTypes writer containsRefsMap
    renderUserTypes typeMap renderFunc

    // plain function wrappers
    for namedFunction in moduleMethods do
        blank()
        write 0 "void %s__wrapper() {" namedFunction.Name
        for argName, argType in namedFunction.Args do
            popValue writer 1 argType (Variable argName)
        let invocation =
            sprintf "%s(%s)" namedFunction.Name (joinedArgNames namedFunction.Args)
        if namedFunction.Throws.IsEmpty then
            pushValue writer 1 namedFunction.Returns invocation (Direct true)
        else
            write 1 "try {"
            pushValue writer 2 namedFunction.Returns invocation (Direct true)
            write 1 "}"
            for exc in namedFunction.Throws do
                match exc with
                | ExceptionType exceptionName ->
                    write 1 "catch (const %s& e) {" exceptionName
                    write 2 "ni_setException(%s);" (lowerFirst exceptionName)
                    pushValue writer 2 exc "e" (Direct true)
                    write 1 "}"
                | _ ->
                    failwithf "C++ plain function wrappers: 'throws' contained non-exception type"
        write 0 "}"

    // interface wrappers
    for name, namedFunctions in interfaces do
        namedFunctions
        |> List.iter (fun nf ->
            blank()
            write 0 "void %s_%s__wrapper(int serverID) {" name nf.Name
            write 1 "auto inst = Server%s::getByID(serverID);" name
            for argName, argType in nf.Args do
                popValue writer 1 argType (Variable argName)
            let invocation =
                sprintf "inst->%s(%s)" nf.Name (joinedArgNames nf.Args)
            if nf.Throws.IsEmpty then
                pushValue writer 1 nf.Returns invocation (Direct true)
            else
                write 1 "try {"
                pushValue writer 2 nf.Returns invocation (Direct true)
                write 1 "}"
                for exc in nf.Throws do
                    match exc with
                    | ExceptionType exceptionName ->
                        write 1 "catch (const %s& e) {" exceptionName
                        write 2 "ni_setException(%s);" (lowerFirst exceptionName)
                        write 2 "%s__push(e);" exceptionName
                        write 1 "}"
                    | _ ->
                        failwithf "C++ interface wrappers: 'throws' contained non-exception type %A" exc
            write 0 "}")

    // globals/constants func
    if globals.Length > 0 then
        blank()
        write 0 "void __constantsFunc() {"
        // push in reverse order
        for globalName, globalType in globals |> List.rev do
            pushValue writer 1 globalType globalName (Direct false) // hmm for isReturn? I guess not because the variable is global and not going anywhere, so it doesn't need to be in the safe area
        write 0 "}"

    // library registration method
    blank()
    write 0 "int %s__register() {" m.Name
    write 1 "auto m = ni_registerModule(\"%s\");" m.Name
    if globals.Length > 0 then
        write 1 "ni_registerModuleConstants(m, &__constantsFunc);"
    // module methods
    for namedFunction in moduleMethods do
        write 1 "ni_registerModuleMethod(m, \"%s\", &%s__wrapper);" namedFunction.Name namedFunction.Name
    // interfaces + methods
    for name, methods in interfaces do
        let lowerName = lowerFirst name
        write 1 "auto %s = ni_registerInterface(m, \"%s\");" lowerName name
        for method in methods do
            write 1 "%s_%s = ni_registerInterfaceMethod(%s, \"%s\", &%s_%s__wrapper);" lowerName method.Name lowerName method.Name name method.Name
    // exceptions
    for name, _ in exceptions do
        let lowerName = lowerFirst name
        write 1 "%s = ni_registerException(m, \"%s\", &%s__buildAndThrow);" lowerName name name
    // done
    write 1 "return 0; // = OK"
    write 0 "}"

let renderHeaderException (writer: IFormatWriter) (exName: string) (exFields: NamedField list) (typeMap: Map<string, UserOrGeneric>) =
    let write, blank = writer.Write, writer.Blank
    //
    write 0 "class %s {" exName
    write 0 "public:"
    for namedField in exFields do
        write 1 "%s %s;" (typeToCppString typeMap namedField.Type_) namedField.Name
    if exFields.IsEmpty then
        write 1 "%s() {}" exName
    else
        let joinedInitializers =
            [for nf in exFields -> sprintf "%s(%s)" nf.Name nf.Name]
            |> String.concat ", "
        write 1 "%s(%s)" exName (joinedFields typeMap exFields)
        write 2 ": %s {}" joinedInitializers
    write 0 "};"

let renderHeaderInterface (writer: IFormatWriter) (name: string) (methods: NamedFunction list) (typeMap: Map<string, UserOrGeneric>) =
    let write, blank = writer.Write, writer.Blank
    //
    write 0 "class %s {" name
    write 0 "public:"
    for namedFunc in methods do
        let throwsComment =
            if namedFunc.Throws.IsEmpty then
                ""
            else
                let joined =
                    namedFunc.Throws
                    |> List.map (typeToCppString typeMap)
                    |> String.concat ", "
                sprintf " // throws %s" joined
        write 1 "virtual %s %s(%s) = 0;%s" (typeToCppString typeMap namedFunc.Returns) namedFunc.Name (joinedArgs typeMap namedFunc.Args) throwsComment
    write 0 "};"
    // server wrapper template
    let outerType =
        InterfaceType name
    blank()
    write 0 "// inherit from this to create server instances of %s" name
    write 0 "class Server%s : public %s, public ServerObject {" (typeToName outerType) name
    write 0 "public:"
    write 1 "virtual ~Server%s() {}" (typeToName outerType)
    write 1 "static std::shared_ptr<Server%s> getByID(int id) {" (typeToName outerType)
    write 2 "auto obj = ServerObject::getByID(id);"
    write 2 "return std::static_pointer_cast<Server%s>(obj);" (typeToName outerType)
    write 1 "}"
    write 0 "};"

let renderHeaderSumType (writer: IFormatWriter) (sumTypeName: string) (values: SumTypeValue list) (typeMap: Map<string, UserOrGeneric>) =
    let write, blank = writer.Write, writer.Blank
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
            write 2 "const %s %s;" (typeToCppString typeMap argType) argName;
        write 2 "static %s make(%s) {" sumTypeName (joinedArgs typeMap value.Args)
        write 3 "%s ret{ Tag::%s };" sumTypeName value.Name
        write 3 "ret.%s = std::shared_ptr<%s>(new %s{ %s });" (lowerFirst value.Name) value.Name value.Name (joinedArgNames value.Args)
        write 3 "return ret;"
        write 2 "}"
        write 1 "};"
    for value in values do
        write 1 "std::shared_ptr<%s> %s;" value.Name (lowerFirst value.Name)
    write 0 "};"
    
let renderHeaderOptionsType (writer: IFormatWriter) (optionsTypeName: string) (namedFields: NamedField list) (typeMap: Map<string, UserOrGeneric>) =
    let write, blank = writer.Write, writer.Blank
    //
    write 0 "struct %s {" optionsTypeName
    write 0 "private:"
    write 1 "enum Fields {"
    for (i, field), maybeComma in namedFields |> List.indexed |> withTrailingCommas do
        write 2 "%s = %d%s" (upperFirst field.Name) (1 <<< i) maybeComma
    write 1 "};"
    let usedFieldsType =
        Primitive (Int(true, Width32))
    write 1 "%s _usedFields;" (typeToCppString typeMap usedFieldsType)
    for field in namedFields do
        write 1 "%s _%s;" (typeToCppString typeMap field.Type_) field.Name
    write 0 "protected:"
    write 1 "%s getUsedFields() {" (typeToCppString typeMap usedFieldsType)
    write 2 "return _usedFields;"
    write 1 "}"
    write 1 "friend void %s__push(%s value, bool isReturn);" (upperFirst optionsTypeName) (upperFirst optionsTypeName)
    write 1 "friend %s %s__pop();" (upperFirst optionsTypeName) (upperFirst optionsTypeName)
    write 0 "public:"
    // accessor methods
    for field in namedFields do
        write 1 "void set%s(%s value) {" (upperFirst field.Name) (typeToCppString typeMap field.Type_)
        write 2 "_%s = value;" field.Name
        write 2 "_usedFields |= Fields::%s;" (upperFirst field.Name)
        write 1 "}"
        write 1 "bool has%s(%s *value) {" (upperFirst field.Name) (typeToCppString typeMap field.Type_)
        write 2 "if (_usedFields & Fields::%s) {" (upperFirst field.Name)
        write 3 "*value = _%s;" field.Name
        write 3 "return true;"
        write 2 "}"
        write 2 "return false;"
        write 1 "}"
    // done
    write 0 "};"
    
let renderHeaderUserTypes (writer: IFormatWriter) (name: string) (content: UserOrGeneric) (typeMap: Map<string, UserOrGeneric>) =
    let write, blank = writer.Write, writer.Blank
    //
    blank()
    match content with
    | UserOrGeneric.FwdInterface ->
        write 0 "class %s; // fwd decl" name
    | UserOrGeneric.Generic gType ->
        match gType with
        | ArrayType _ ->
            write 0 "// %s" (typeToCppString typeMap gType) // remove this eventually
        | MapType _ ->
            write 0 "// %s" (typeToCppString typeMap gType) // likewise
        | TupleType _ ->
            write 0 "// %s" (typeToCppString typeMap gType) // !
        | BufferType _ ->
            write 0 "// %s" (typeToCppString typeMap gType)
        | SetType _ ->
            write 0 "// %s" (typeToCppString typeMap gType)
        | _ ->
            failwithf "renderUserTypes: unsupported generic type %A" gType
    | UserOrGeneric.User userType ->
        match userType with
        | OpaqueHandleDef ->
            write 0 "struct __%s; typedef struct __%s* %s;" name name name
        | StructDef namedFields ->
            write 0 "struct %s {" name
            for field in namedFields do
                write 1 "%s %s;" (typeToCppString typeMap field.Type_) field.Name
            write 0 "};"
        | OptionsDef namedFields ->
            renderHeaderOptionsType writer name namedFields typeMap
        | ExceptionDef namedFields ->
            renderHeaderException writer name namedFields typeMap
        | InterfaceDef namedFunctions ->
            renderHeaderInterface writer name namedFunctions typeMap
        | DelegateDef info ->
            let maybeThrows =
                if info.Throws.IsEmpty then
                    ""
                else
                    let joined =
                        info.Throws
                        |> List.map (typeToCppString typeMap)
                        |> String.concat ", "
                    sprintf " // throws %s" joined
            write 0 "typedef %s %s(%s);%s" (typeToCppString typeMap info.Returns) name (joinedArgs typeMap info.Args) maybeThrows
        | TypeDef actual ->
            write 0 "typedef %s %s;" (typeToCppString typeMap actual) name
        | EnumDef info ->
            let maybeWidth =
                match info.MaybeWidth with
                | Some widthType ->
                    sprintf " : %s" (typeToCppString typeMap widthType)
                | None ->
                    ""
            if info.FlagsMode then
                write 0 "enum %s%s {" name maybeWidth
            else
                write 0 "enum class %s%s {" name maybeWidth
            for (value, maybeSpecifiedValue), maybeComma in info.Values |> withTrailingCommas do
                match maybeSpecifiedValue with
                | Some specValue ->
                    write 1 "%s = %d%s" value specValue maybeComma
                | None ->
                    write 1 "%s%s" value maybeComma
            write 0 "};"
        | SumTypeDef enumValues ->
            renderHeaderSumType writer name enumValues typeMap 

let generateImplementationH (m: Module) (typeMap: Map<string, UserOrGeneric>) (writer: IFormatWriter) (maybeModulePrefix: string option) =
    let write, blank = writer.Write, writer.Blank
    //
    let globals =
        m.Defs
        |> List.choose (function
            | Global(name, niType) -> Some (name, niType)
            | _ -> None)
    let moduleMethods =
        m.Defs
        |> List.choose (function
            | FunctionDef namedFunction -> Some namedFunction
            | _ -> None)
    // begin output: ===============================================
    write 0 "#pragma once"
    blank ()
    write 0 "#include \"%s\"" (relativeIncludeFilename maybeModulePrefix "$PARENT.support.NativeImplServer")
    write 0 "#include <functional>"
    write 0 "#include <memory>"
    write 0 "#include <string>"
    write 0 "#include <vector>"
    write 0 "#include <map>"
    write 0 "#include <tuple>"
    write 0 "#include <set>"
    
    // imports
    if not m.Imports.IsEmpty then
        blank()
        for importName in m.Imports do
            write 0 "#include \"%s.h\"" (importName.Replace(".", "/"))

    // all user types in dependency order
    renderUserTypes typeMap (renderHeaderUserTypes writer)

    // global externs
    if globals.Length > 0 then
        blank()
        for globalName, globalType in globals do
            write 0 "extern const %s %s;" (typeToCppString typeMap globalType) globalName

    // function prototypes
    if moduleMethods.Length > 0 then
        blank()
        for namedFunc in moduleMethods do
            let throwsComment =
                if namedFunc.Throws.IsEmpty then
                    ""
                else
                    let joined =
                        namedFunc.Throws
                        |> List.map (typeToCppString typeMap)
                        |> String.concat ", "
                    sprintf " // throws %s" joined
            write 0 "%s %s(%s);%s" (typeToCppString typeMap namedFunc.Returns) namedFunc.Name (joinedArgs typeMap namedFunc.Args) throwsComment
        
    // registration method (called by Library.cpp or wherever we centralize this ...)
    blank()
    write 0 "int %s__register();" m.Name

let generateImplementationCpp (m: Module) (typeMap: Map<string, UserOrGeneric>) (writer: IFormatWriter) (className: string) =
    let write, blank = writer.Write, writer.Blank
    //
    let globals =
        m.Defs
        |> List.choose (function
            | Global(name, niType) -> Some (name, niType)
            | _ -> None)
    let opaqueHandles =
        m.Defs
        |> List.choose (function
            | UserType(name, OpaqueHandleDef) -> Some name
            | _ -> None)
    let moduleMethods =
        m.Defs
        |> List.choose (function
            | FunctionDef namedFunction -> Some namedFunction
            | _ -> None)
    // begin output: ===============================================
    // right now this is hardcoded to the root level - we're not using any of the complex "outParams" stuff that the header/wrappers functions are using
    // it's quite a big chunk of code at this point and it needs to be extracted and cleaned up
    // but that's a day's worth of trouble, so not right now ...
    write 0 "#include \"generated/%s.h\"" className

    // globals
    if globals.Length > 0 then
        blank()
        for globalName, globalType in globals do
            write 0 "const %s %s = %s;" (typeToCppString typeMap globalType) globalName (defaultValueForType typeMap globalType)
    // opaque handles
    if opaqueHandles.Length > 0 then
        blank()
        for name in opaqueHandles do
            write 0 "struct __%s {" (upperFirst name)
            write 1 "// your content here"
            write 0 "};"
    // functions
    for namedFunc in moduleMethods do
        blank()
        let throwsComment =
            if namedFunc.Throws.IsEmpty then
                ""
            else
                let joined =
                    namedFunc.Throws
                    |> List.map (typeToCppString typeMap)
                    |> String.concat ", "
                sprintf " // throws %s" joined
        write 0 "%s %s(%s) {%s" (typeToCppString typeMap namedFunc.Returns) namedFunc.Name (joinedArgs typeMap namedFunc.Args) throwsComment
        if namedFunc.Returns <> UnitType then
            write 1 "return %s;" (defaultValueForType typeMap namedFunc.Returns)
        write 0 "}"
        
let projectDir (outParams: OutParams) =
    sprintf "%s/server/cpp/%sImpl" outParams.Path outParams.LibraryName
    
let writeLibrary (allModules: Module list) (writer: IFormatWriter) = // (maybeModulePrefix: string option) =
    let write, blank = writer.Write, writer.Blank

    for m in allModules do
        write 0 "#include \"%s\"" (relativeIncludeFilename None m.Name)
    blank()
    write 0 "extern \"C\" int nativeLibraryInit() {"
    for m in allModules do
        write 1 "%s__register();" m.Name
    write 1 "// should we do module inits here as well?"
    write 1 "// currently they are manually done on the C# side inside the <module>.Init methods (which perform registration first) - and those are individually called by Library.Init, which first calls nativeImplInit"
    write 1 "return 0;"
    write 0 "}"
    blank()
    write 0 "extern \"C\" void nativeLibraryShutdown() {"
    write 1 "// module shutdowns? see note above"
    write 0 "}"
    
let generateLibrary (allModules: Module list) (outParams: OutParams) =
    let baseNamespace, maybeModulePrefix, className =
        namespaceParts outParams "Library"
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
        new SimpleFormatWriter(writer)
    writeLibrary allModules libraryWriter
    
    
let generateModule (outParams: OutParams) (m: Module) =
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
    
    let wrappersFilename =
        $"{generatedFilenameNoExt}_Wrappers.cpp"
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
            
    let typeMap =
        userTypesAndGenerics m
        
    printfn "  - %s" wrappersFilename
    use wrappersOutput =
        let writer = new StreamWriter(wrappersFilename)
        new SimpleFormatWriter(writer)
    generateWrappersCpp m wrappersOutput maybeModulePrefix className

    printfn "  - %s" implementationHeaderFilename
    use implHeaderOutput =
        let writer = new StreamWriter(implementationHeaderFilename)
        new SimpleFormatWriter(writer)
    generateImplementationH m typeMap implHeaderOutput maybeModulePrefix
    
    printfn "  - %s" implementationFilename
    use implementationOutput =
        let writer = new StreamWriter(implementationFilename)
        new SimpleFormatWriter(writer)
    generateImplementationCpp m typeMap implementationOutput className
