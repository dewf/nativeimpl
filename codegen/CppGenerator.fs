module CppGenerator

open System.IO
open System.Text.RegularExpressions
open Parser

open Util
open Common

let PopTemplate = "std::shared_ptr<%%IFACE%%> %%IFACE%%__pop()
{
    bool isClientID, inSafetyArea;
    auto id = ni_popInstance(&isClientID, &inSafetyArea);
    if (isClientID) {
        return std::shared_ptr<%%IFACE%%>(new Client%%IFACE%%(id));
    }
    else {
        return Server%%IFACE%%::getByID(id);
    }
}"
let PushTemplate = "inline void %%IFACE%%__push(std::shared_ptr<%%IFACE%%> inst, bool isReturn) {
    auto pushable = std::dynamic_pointer_cast<Pushable>(inst);
    pushable->push(pushable, isReturn);
}"
let ServerWrapperTemplate = "// inherit from this to create server instances of %%IFACE%%
class Server%%IFACE%% : public %%IFACE%%, public ServerObject {
public:
    virtual ~Server%%IFACE%%() {}
    static std::shared_ptr<Server%%IFACE%%> getByID(int id) {
        auto obj = ServerObject::getByID(id);
        return std::static_pointer_cast<Server%%IFACE%%>(obj);
    }
};"

let rec typeToName = function
    | UnitType -> "Unit"
    | Primitive prim ->
        match prim with
        | SizeT -> "SizeT"
        | Bool -> "Bool"
        | Int32 -> "Int"
        | Float32 -> "Float"
        | Float64 -> "Double"
        | StringType -> "String"
        | x -> sprintf "(typeToName: primitive not done yet [%A]" x
    | OpaqueHandleType name -> upperFirst name
    | InterfaceType name -> upperFirst name
    | ExceptionType name -> upperFirst name
    | DelegateType name -> upperFirst name
    | ArrayType niType -> sprintf "%sArray" (typeToName niType)
    | MapType (keyType, valueType) -> sprintf "%s%sMap" (typeToName keyType) (typeToName valueType)
    | TupleType tupleTypes -> sprintf "%sTuple" (tupleTypes |> List.map typeToName |> String.concat "")
    | BufferType elemType -> sprintf "Native%sBuffer" (typeToName elemType)
    | StructType name -> upperFirst name
    | TypedefType name -> upperFirst name
    | EnumType name -> upperFirst name
    | SumType name -> upperFirst name
    | x -> sprintf "(typeToName: not done yet [%A]" x

let rec typeToCppString = function
    | UnitType -> "void"
    | Primitive prim ->
        match prim with
        | SizeT -> "size_t"
        | Bool -> "bool"
        | Int32 -> "int"
        | Float32 -> "float"
        | Float64 -> "double"
        | StringType -> "std::string"
        | x -> sprintf "(typeToCppString: primitive not done yet [%A])" x
    | OpaqueHandleType name -> name
    | InterfaceType name -> sprintf "std::shared_ptr<%s>" name
    | ExceptionType name -> name
    | DelegateType name -> sprintf "std::function<%s>" name
    | ArrayType niType -> sprintf "std::vector<%s>" (typeToCppString niType)
    | MapType (keyType, valueType) -> sprintf "std::map<%s,%s>" (typeToCppString keyType) (typeToCppString valueType)
    | TupleType tupleTypes -> sprintf "std::tuple<%s>" (tupleTypes |> List.map typeToCppString |> String.concat ",")
    | BufferType elemType -> sprintf "std::shared_ptr<NativeBuffer<%s>>" (typeToCppString elemType)
    | StructType name -> name
    | TypedefType name -> name
    | EnumType name -> name
    | SumType name -> name
    | x -> sprintf "(typeToCppString: not done yet [%A])" x

let joinedFields (fields: NamedField list) =
    [for field in fields -> sprintf "%s %s" (typeToCppString field.Type_) field.Name]
    |> String.concat ", "

let joinedFieldNames (fields: NamedField list) =
    [for field in fields -> field.Name]
    |> String.concat ", "

let joinedArgs (args: (string * NIType) list) =
    [for name, type_ in args -> sprintf "%s %s" (typeToCppString type_) name]
    |> String.concat ", "

let joinedArgNames (args: (string * NIType) list) =
    args
    |> List.map fst
    |> String.concat ", "

let rec defaultValueForType = function
    | UnitType -> failwith "defaultValueForType called on UnitType"
    | Primitive prim ->
        match prim with
        | SizeT -> "0"
        | Bool -> "false"
        | Int32 -> "0"
        | Float32 -> "0.0f"
        | Float64 -> "0.0"
        | StringType -> "\"default\""
        | _ ->
            "(defaultValueForType: not yet supported)"
    | OpaqueHandleType name -> sprintf "new __%s()" (upperFirst name)
    | InterfaceType name -> sprintf "std::shared_ptr<%s>()" name
    | DelegateType name -> sprintf "std::function<%s>()" name
    | ArrayType niType -> sprintf "std::vector<%s>()" (typeToCppString niType)
    | MapType (keyType, valueType) -> sprintf "std::map<%s,%s>()" (typeToCppString keyType) (typeToCppString valueType)
    | BufferType elemType -> sprintf "std::shared_ptr<NativeBuffer<%s>>()" (typeToCppString elemType)
    | TupleType tupleTypes ->
        let typesJoined =
            tupleTypes |> List.map typeToCppString |> String.concat ","
        let defaultsJoined =
            tupleTypes |> List.map defaultValueForType |> String.concat ", "
        sprintf "std::tuple<%s>{ %s }" typesJoined defaultsJoined
    | _ ->
        "(defaultValueForType: not yet supported)"

type PopValueContext =
    | Variable of name: string
    | Return
    | SafeReturn
    | ExceptionContext

let popValue (writer: TextWriter) (level: int) (type_: NIType) (context: PopValueContext) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let contextPrefix =
        match context with
        | Variable varName -> sprintf "auto %s =" varName
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
        | SizeT ->
            write level "%s ni_popSizeT();" contextPrefix
        | Bool ->
            write level "%s ni_popBool();" contextPrefix
        | Int32 ->
            write level "%s ni_popInt();" contextPrefix
        | Float32 ->
            write level "%s ni_popFloat();" contextPrefix
        | Float64 ->
            write level "%s ni_popDouble();" contextPrefix
        | StringType ->
            write level "%s popStringInternal();" contextPrefix
        | _ ->
            write level "// (pop not yet implemented: prim %A)" prim
    | OpaqueHandleType name ->
        write level "%s (%s)ni_popSizeT();" contextPrefix (upperFirst name)
    | InterfaceType ifaceName ->
        write level "%s %s__pop();" contextPrefix ifaceName
    | DelegateType delegateName ->
        write level "%s %s__pop();" contextPrefix delegateName
    | ArrayType niType ->
        let popFunc =
            match niType with
            | Primitive prim ->
                match prim with
                | Bool -> "popBoolArrayInternal"
                | Int32 -> "popIntArrayInternal"
                | Float32 -> "popFloatArrayInternal"
                | Float64 -> "popDoubleArrayInternal"
                | StringType -> "popStringArrayInternal"
                | _ -> sprintf "// (pop not yet supported: prim %A)" prim
            | StructType name -> sprintf "__%sArray__pop" name
            | TypedefType name -> sprintf "__%sArray__pop" name
            | DelegateType name -> sprintf "__%sArray__pop" name
            | ArrayType _ -> sprintf "__%sArray__pop" (typeToName niType)
            | EnumType name -> sprintf "__%sArray__pop" name
            | SumType name -> sprintf "__%sArray__pop" name
            | TupleType _ -> sprintf "__%sArray__pop" (typeToName niType)
            | MapType _ -> sprintf "__%sArray__pop" (typeToName niType)
            | _ -> sprintf "// (pop not yet supported: %A)" type_
        write level "%s %s();" contextPrefix popFunc
    | MapType (keyType, valueType) ->
        write level "%s __%s%sMap__pop();" contextPrefix (typeToName keyType) (typeToName valueType)
    | TupleType _ ->
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
    | ExceptionType exceptionName ->
        write level "%s__buildAndThrow();" exceptionName
    | _ ->
        write level "// (pop not yet implemented: %A)" type_
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
        write level "%s;" content // no push!
    | Primitive prim ->
        match prim with
        | SizeT ->
            write level "ni_pushSizeT(%s);" content
        | Bool ->
            write level "ni_pushBool(%s);" content
        | Int32 ->
            write level "ni_pushInt(%s);" content
        | Float32 ->
            write level "ni_pushFloat(%s);" content
        | Float64 ->
            write level "ni_pushDouble(%s);" content
        | StringType ->
            write level "pushStringInternal(%s);" content
        | _ ->
            write level "// (pushValue not supported yet: prim %A)" prim
    | OpaqueHandleType _ ->
        write level "ni_pushSizeT((size_t)%s);" content
    | InterfaceType name ->
        write level "%s__push(%s, %s);" name content (isReturnString())
    | DelegateType name ->
        write level "%s__push(%s);" name content // still hmm about safety area for functions ...
    | ArrayType niType ->
        match niType with
        | Primitive prim ->
            match prim with
            | Bool ->
                write level "pushBoolArrayInternal(%s);" content
            | Int32 ->
                write level "pushIntArrayInternal(%s);" content
            | Float32 ->
                write level "pushFloatArrayInternal(%s);" content
            | Float64 ->
                write level "pushDoubleArrayInternal(%s);" content
            | StringType ->
                write level "pushStringArrayInternal(%s);" content
            | _ ->
                write level "// (push not yet supported: array of prim %A)" prim
        | ArrayType niType ->
            write level "__%sArrayArray__push(%s, %s);" (typeToName niType) content (isReturnString())
        | MapType (keyType, valueType) ->
            write level "__%s%sMapArray__push(%s, %s);" (typeToName keyType) (typeToName valueType) content (isReturnString())
        | TupleType _ ->
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
    | ExceptionType name ->
        assert (isReturn = Direct true)          // maybe needs a separate value for exceptions?
        write level "%s__push(%s);" name content // isreturn irrelevant, exceptions must push everything safely because they are basically returns
    | _ ->
        write level "// (pushValue not supported yet: %A)" type_


let renderWrapperDelegate (writer: TextWriter) (name: string) (info: DelegateInfo) (typeContainsRefs: NIType -> bool) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""
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
                write 2 "catch (const %s& e) {" (typeToCppString exc)
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
    write 1 "auto wrapper = [cf](%s) {" (joinedArgs info.Args)
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

let renderWrapperInterface (writer: TextWriter) (name: string) (methods: NamedFunction list) (typeContainsRefs: NIType -> bool) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""
    // client wrapper
    write 0 "class Client%s : public ClientObject, public %s {" name name
    write 0 "public:"
    write 1 "Client%s(int id) : ClientObject(id) {}" name
    // methods
    for namedFunc in methods do
        write 1 "%s %s(%s) override {" (typeToCppString namedFunc.Returns) namedFunc.Name (joinedArgs namedFunc.Args)
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
    blank()
    // push/pop
    let nameRegex = Regex(@"%%IFACE%%")
    writer.WriteLine(nameRegex.Replace(PushTemplate, name))
    writer.WriteLine()
    writer.WriteLine(nameRegex.Replace(PopTemplate, name))

let renderWrapperException (writer: TextWriter) (name: string) (exFields: NamedField list) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""
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

let renderWrapperSumType (writer: TextWriter) (sumTypeName: string) (values: SumTypeValue list) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""
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
    pushValue writer 1 (Primitive Int32) "(int)value.tag" (Indirect "isReturn")
    write 0 "}"
    // pop
    blank()
    write 0 "%s %s__pop() {" sumTypeName sumTypeName
    popValue writer 1 (Primitive Int32) (Variable "which")
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

let renderWrapperUserTypes (writer: TextWriter) (containsRefsMap: Map<string,bool>) (name: string) (content: UserOrGeneric) (typeMap: Map<string, UserOrGeneric>) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""
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
            write 0 "inline void __%s__push(%s buf, bool isReturn) {" (typeToName gType) (typeToCppString gType)
            write 1 "auto pushable = std::dynamic_pointer_cast<Pushable>(buf);"
            write 1 "pushable->push(pushable, isReturn);"
            write 0 "}"
            // POP ======
            blank()
            write 0 "%s __%s__pop() {" (typeToCppString gType) (typeToName gType)
            write 1 "int id;"
            write 1 "bool isClientId, inSafetyArea;"
            write 1 "ni_BufferDescriptor descriptor;"
            write 1 "ni_popBuffer(&id, &isClientId, &inSafetyArea, &descriptor);"
            write 1 "if (isClientId) {"
            write 2 "auto buf = new ClientBuffer<%s>(id, descriptor);" (typeToCppString elemType)
            write 2 "return %s(buf);" (typeToCppString gType)
            write 1 "}"
            write 1 "else {"
            write 2 "return ServerBuffer<%s>::getByID(id);" (typeToCppString elemType)
            write 1 "}"
            write 0 "}"
        | TupleType tupleTypes ->
            // PUSH =====
            write 0 "void __%s__push(%s value, bool isReturn) {" (typeToName gType) (typeToCppString gType)
            for i, type_ in tupleTypes |> List.indexed |> List.rev do
                pushValue writer 1 type_ (sprintf "std::get<%d>(value)" i) (Indirect "isReturn")
            write 0 "}"
            // POP ======
            blank()
            write 0 "%s __%s__pop() {" (typeToCppString gType) (typeToName gType)
            for i, type_ in tupleTypes |> List.indexed do
                popValue writer 1 type_ (Variable (sprintf "_%d" i))
            let itemsJoined =
                tupleTypes
                |> List.indexed
                |> List.map (fun (i, _) -> sprintf "_%d" i)
                |> String.concat ", "
            write 1 "return %s{ %s };" (typeToCppString gType) itemsJoined
            write 0 "}"
        | MapType (keyType, valueType) ->
            // PUSH =====
            write 0 "void __%s__push(%s _map, bool isReturn) {" (typeToName gType) (typeToCppString gType)
            write 1 "std::vector<%s> keys;" (typeToCppString keyType)
            write 1 "std::vector<%s> values;" (typeToCppString valueType)
            write 1 "for (auto i = _map.begin(); i != _map.end(); i++) {"
            write 2 "keys.push_back(i->first);"
            write 2 "values.push_back(i->second);"
            write 1 "}"
            pushValue writer 1 (ArrayType valueType) "values" (Indirect "isReturn")
            pushValue writer 1 (ArrayType keyType) "keys" (Indirect "isReturn")
            write 0 "}"
            // POP =====
            blank()
            write 0 "%s __%s__pop() {" (typeToCppString gType) (typeToName gType)
            write 1 "%s __ret;" (typeToCppString gType)
            popValue writer 1 (ArrayType keyType) (Variable "keys")
            popValue writer 1 (ArrayType valueType) (Variable "values")
            write 1 "for (auto i = 0; i < keys.size(); i++) {"
            write 2 "__ret[keys[i]] = values[i];"
            write 1 "}"
            write 1 "return __ret;"
            write 0 "}"
        | ArrayType arrayType ->
            let arrayCommon (name: string) (elementType: NIType) =
                // use this for any types with no particular specializations (typedefs, delegates, arrays of arrays -- everything but structs and tuples, basically)
                // PUSH ========
                write 0 "void __%sArray__push(std::vector<%s> values, bool isReturn) {" name (typeToCppString elementType)
                write 1 "for (auto v = values.rbegin(); v != values.rend(); v++) {" // in reverse!
                pushValue writer 2 elementType "*v" (Indirect "isReturn")
                write 1 "}"
                write 1 "ni_pushSizeT(values.size());"
                write 0 "}"
                // POP =========
                blank()
                write 0 "std::vector<%s> __%sArray__pop() {" (typeToCppString elementType) name
                write 1 "std::vector<%s> __ret;" (typeToCppString elementType)
                write 1 "auto count = ni_popSizeT();"
                write 1 "for (auto i = 0; i < count; i++) {"
                popValue writer 2 elementType (Variable "value")
                write 2 "__ret.push_back(value);"
                write 1 "}"
                write 1 "return __ret;"
                write 0 "}"
            let tupleArray (tupleTypes: NIType list) =
                // PUSH ========
                write 0 "void __%sArray__push(%s values, bool isReturn) {" (typeToName arrayType) (typeToCppString gType)
                for i, type_ in tupleTypes |> List.indexed do
                    write 1 "std::vector<%s> _%dvalues;" (typeToCppString type_) i
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
                write 0 "%s __%sArray__pop() {" (typeToCppString gType) (typeToName arrayType)
                for i, type_ in tupleTypes |> List.indexed do
                    popValue writer 1 (ArrayType type_) (Variable (sprintf "_%dvalues" i))
                write 1 "%s __ret;" (typeToCppString gType)
                write 1 "for (auto i = 0; i < _0values.size(); i++) {"
                let joined =
                    tupleTypes |> List.indexed |> List.map fst |> List.map (sprintf "_%dvalues[i]") |> String.concat ", "
                write 2 "__ret.push_back({ %s });" joined
                write 1 "}"
                write 1 "return __ret;"
                write 0 "}"
            match arrayType with
            | TupleType tupleTypes ->
                tupleArray tupleTypes
            | StructType structName ->
                // fetch the fields so we can make a struct-of-arrays
                match typeMap[structName] with
                | UserOrGeneric.User (StructDef namedFields) ->
                    // PUSH ===========
                    write 0 "void __%sArray__push(%s values, bool isReturn) {" structName (typeToCppString gType)
                    for field in namedFields |> List.rev do
                        write 1 "std::vector<%s> %s_values;" (typeToCppString field.Type_) field.Name
                    write 1 "for (auto v = values.begin(); v != values.end(); v++) {"
                    for field in namedFields |> List.rev do
                        write 2 "%s_values.push_back(v->%s);" field.Name field.Name
                    write 1 "}"
                    for field in namedFields |> List.rev do
                        pushValue writer 1 (ArrayType field.Type_) (sprintf "%s_values" field.Name) (Indirect "isReturn")
                    write 0 "}"
                    // POP ============
                    blank()
                    write 0 "%s __%sArray__pop() {" (typeToCppString gType) structName
                    for field in namedFields do
                        popValue writer 1 (ArrayType field.Type_) (Variable (sprintf "%s_values" field.Name))
                    write 1 "std::vector<%s> __ret;" structName
                    // use first field to iterate
                    write 1 "for (auto i = 0; i < %s_values.size(); i++) {" namedFields.Head.Name
                    write 2 "%s __value;" structName
                    for field in namedFields do
                        write 2 "__value.%s = %s_values[i];" field.Name field.Name
                    write 2 "__ret.push_back(__value);"
                    write 1 "}"
                    write 1 "return __ret;"
                    write 0 "}"
                | _ ->
                    failwithf "renderWrapperUserOrGeneric: failed to find struct in typemap"
            | TypedefType name ->
                // flatten array of typedef of tuple, to array of tuple
                match typeMap.TryFind(name) with
                | Some uog ->
                    match uog with
                    | UserOrGeneric.User (TypeDef (TupleType tupleTypes)) ->
                        tupleArray tupleTypes
                    | _ ->
                        // any other typedef in an array context
                        arrayCommon name arrayType
                | None ->
                    failwith "renderWrapperUserTypes: array of typedef: didn't find typedef name in map??"
            | DelegateType name ->
                arrayCommon name arrayType
            | ArrayType _ ->
                arrayCommon (typeToName arrayType) arrayType
            | EnumType name ->
                // PUSH ================
                write 0 "void __%sArray__push(std::vector<%s> values, bool isReturn) {" name name
                write 1 "std::vector<int> intValues;"
                write 1 "for (auto i = values.begin(); i != values.end(); i++) {"
                write 2 "intValues.push_back((int)*i);"
                write 1 "}"
                write 1 "pushIntArrayInternal(intValues);"
                write 0 "}"
                // POP ==================
                blank()
                write 0 "std::vector<%s> __%sArray__pop() {" name name
                write 1 "auto intValues = popIntArrayInternal();"
                write 1 "std::vector<%s> __ret;" name
                write 1 "for (auto i = 0; i < intValues.size(); i++) {"
                write 2 "__ret.push_back((%s)intValues[i]);" name
                write 1 "}"
                write 1 "return __ret;"
                write 0 "}"
            | SumType name ->
                arrayCommon name arrayType
            | MapType _ ->
                arrayCommon (typeToName arrayType) arrayType
            // ignore built-ins below:
            | Primitive _ ->
                write 0 "// built-in array type: %s" (typeToCppString gType)
            | _ ->
                write 0 "// TODO: push/pop for generic %s" (typeToCppString gType)
        | _ ->
            write 0 "// push/pop for generic %s" (typeToCppString gType)
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
        | ExceptionDef namedFields ->
            renderWrapperException writer name namedFields
        | InterfaceDef methods ->
            renderWrapperInterface writer name methods typeContainsRefs
        | DelegateDef info ->
            renderWrapperDelegate writer name info typeContainsRefs
        | TypeDef niType ->
            // ghetto, but for now instead of flattening, just write an inline wrapper
            write 0 "inline %s %s__pop() {" name name
            popValue writer 1 niType Return
            write 0 "}"
            blank()
            write 0 "inline void %s__push(%s value, bool isReturn) {" name name
            pushValue writer 1 niType "value" (Indirect "isReturn")
            write 0 "}"
        | EnumDef _ ->
            write 0 "inline void %s__push(%s value) {" name name
            pushValue writer 1 (Primitive Int32) "(int)value" NotApplicable
            write 0 "}"
            blank()
            write 0 "inline %s %s__pop() {" name name
            popValue writer 1 (Primitive Int32) (Variable "tag")
            write 1 "return (%s)tag;" name
            write 0 "}"
        | SumTypeDef values ->
            renderWrapperSumType writer name values

let generateWrappersCpp (m: Module) (writer: TextWriter) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""
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
    write 0 "#include \"NativeImplServer.h\""
    write 0 "#include \"Implementation.h\""

    // opaques + interface method + exception handles (for registration storage)
    blank()
    for name, userType in userTypes do
        let lowerName = lowerFirst name
        match userType with
//        | OpaqueHandleDef ->
//            write 0 "NIHANDLE(%s);" lowerName
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

    // library entry point / registrations
    blank()
    write 0 "extern \"C\" int nativeLibraryInit() {"
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
    write 0 ""
    // shutdown
    write 0 "extern \"C\" void nativeLibraryShutdown() {"
    // nothing for C++
    write 0 "}"

let renderHeaderException (writer: TextWriter) (exName: string) (exFields: NamedField list) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    write 0 "class %s {" exName
    write 0 "public:"
    for namedField in exFields do
        write 1 "%s %s;" (typeToCppString namedField.Type_) namedField.Name
    if exFields.IsEmpty then
        write 1 "%s() {}" exName
    else
        let joinedInitializers =
            [for nf in exFields -> sprintf "%s(%s)" nf.Name nf.Name]
            |> String.concat ", "
        write 1 "%s(%s)" exName (joinedFields exFields)
        write 2 ": %s {}" joinedInitializers
    write 0 "};"

let renderHeaderInterface (writer: TextWriter) (name: string) (methods: NamedFunction list) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    write 0 "class %s {" name
    write 0 "public:"
    for namedFunc in methods do
        let throwsComment =
            if namedFunc.Throws.IsEmpty then
                ""
            else
                let joined =
                    namedFunc.Throws
                    |> List.map typeToCppString
                    |> String.concat ", "
                sprintf " // throws %s" joined
        write 1 "virtual %s %s(%s) = 0;%s" (typeToCppString namedFunc.Returns) namedFunc.Name (joinedArgs namedFunc.Args) throwsComment
    write 0 "};"
    write 0 ""
    // server wrapper
    let nameRegex = Regex(@"%%IFACE%%")
    writer.WriteLine(nameRegex.Replace(ServerWrapperTemplate, name))

let renderHeaderSumType (writer: TextWriter) (sumTypeName: string) (values: SumTypeValue list) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
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
            write 2 "const %s %s;" (typeToCppString argType) argName;
        write 2 "static %s make(%s) {" sumTypeName (joinedArgs value.Args)
        write 3 "%s ret{ Tag::%s };" sumTypeName value.Name
        write 3 "ret.%s = std::shared_ptr<%s>(new %s{ %s });" (lowerFirst value.Name) value.Name value.Name (joinedArgNames value.Args)
        write 3 "return ret;"
        write 2 "}"
        write 1 "};"
    for value in values do
        write 1 "std::shared_ptr<%s> %s;" value.Name (lowerFirst value.Name)
    write 0 "};"

let renderHeaderUserTypes (writer: TextWriter) (name: string) (content: UserOrGeneric) (_typeMap: Map<string, UserOrGeneric>) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""
    blank()
    match content with
    | UserOrGeneric.FwdInterface ->
        write 0 "class %s; // fwd decl" name
    | UserOrGeneric.Generic gType ->
        match gType with
        | ArrayType _ ->
            write 0 "// %s" (typeToCppString gType) // remove this eventually
        | MapType _ ->
            write 0 "// %s" (typeToCppString gType) // likewise
        | TupleType _ ->
            write 0 "// %s" (typeToCppString gType) // !
        | BufferType _ ->
            write 0 "// %s" (typeToCppString gType)
        | _ ->
            failwithf "renderUserTypes: unsupported generic type %A" gType
    | UserOrGeneric.User userType ->
        match userType with
        | OpaqueHandleDef ->
            write 0 "struct __%s; typedef struct __%s* %s;" name name name
        | StructDef namedFields ->
            write 0 "struct %s {" name
            for field in namedFields do
                write 1 "%s %s;" (typeToCppString field.Type_) field.Name
            write 0 "};"
        | ExceptionDef namedFields ->
            renderHeaderException writer name namedFields
        | InterfaceDef namedFunctions ->
            renderHeaderInterface writer name namedFunctions
        | DelegateDef info ->
            let maybeThrows =
                if info.Throws.IsEmpty then
                    ""
                else
                    let joined =
                        info.Throws
                        |> List.map typeToCppString
                        |> String.concat ", "
                    sprintf " // throws %s" joined
            write 0 "typedef %s %s(%s);%s" (typeToCppString info.Returns) name (joinedArgs info.Args) maybeThrows
        | TypeDef actual ->
            write 0 "typedef %s %s;" (typeToCppString actual) name
        | EnumDef values ->
            write 0 "enum class %s {" name
            for value, maybeComma in values |> withTrailingCommas do
                write 1 "%s%s" value maybeComma
            write 0 "};"
        | SumTypeDef enumValues ->
            renderHeaderSumType writer name enumValues

let generateImplementationH (m: Module) (writer: TextWriter) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""
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
    write 0 "#include \"NativeImplServer.h\""
    write 0 "#include <functional>"
    write 0 "#include <memory>"
    write 0 "#include <string>"
    write 0 "#include <vector>"
    write 0 "#include <map>"
    write 0 "#include <tuple>"

    // all user types in dependency order
    renderUserTypes (userTypesAndGenerics m) (renderHeaderUserTypes writer)

    // global externs
    if globals.Length > 0 then
        blank()
        for globalName, globalType in globals do
            write 0 "extern const %s %s;" (typeToCppString globalType) globalName

    // function prototypes
    blank()
    for namedFunc in moduleMethods do
        let throwsComment =
            if namedFunc.Throws.IsEmpty then
                ""
            else
                let joined =
                    namedFunc.Throws
                    |> List.map typeToCppString
                    |> String.concat ", "
                sprintf " // throws %s" joined
        write 0 "%s %s(%s);%s" (typeToCppString namedFunc.Returns) namedFunc.Name (joinedArgs namedFunc.Args) throwsComment

let generateImplementationCpp (m: Module) (writer: TextWriter) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""
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
    write 0 "#include \"Implementation.h\""
    // globals
    if globals.Length > 0 then
        blank()
        for globalName, globalType in globals do
            write 0 "const %s %s = %s;" (typeToCppString globalType) globalName (defaultValueForType globalType)
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
                    |> List.map typeToCppString
                    |> String.concat ", "
                sprintf " // throws %s" joined
        write 0 "%s %s(%s) {%s" (typeToCppString namedFunc.Returns) namedFunc.Name (joinedArgs namedFunc.Args) throwsComment
        if namedFunc.Returns <> UnitType then
            write 1 "return %s;" (defaultValueForType namedFunc.Returns)
        write 0 "}"

let generate (m: Module) =
    printfn "C++ writing Wrappers.cpp"
    use wrappersCpp = new StreamWriter(@"F:\Workbench\nativeimpl\server\cpp_generated\TestLibraryCpp\Wrappers.cpp")
    generateWrappersCpp m wrappersCpp

    printfn "C++ writing Implementation.h"
    use implementationH = new StreamWriter(@"F:\Workbench\nativeimpl\server\cpp_generated\TestLibraryCpp\Implementation.h")
    generateImplementationH m implementationH

    printfn "C++ writing Implementation.cpp"
    use implementationCpp = new StreamWriter(@"F:\Workbench\nativeimpl\server\cpp_generated\TestLibraryCpp\Implementation.cpp")
    generateImplementationCpp m implementationCpp
