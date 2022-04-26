module CppGenerator

open System.IO
open System.Text.RegularExpressions
open Parser

open Util

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
    | Bool -> "Bool"
    | Int32 -> "Int"
    | StringType -> "String"
    | InterfaceType name -> upperFirst name
    | ExceptionType name -> upperFirst name
    | DelegateType name -> upperFirst name
    | ArrayType niType -> sprintf "%sArray" (typeToName niType)
    | MapType (keyType, valueType) -> sprintf "%s%sMap" (typeToName keyType) (typeToName valueType)
    | StructType name -> upperFirst name
    | TypedefType name -> upperFirst name
    | EnumType name -> upperFirst name
    | SumType name -> upperFirst name
    | x -> sprintf "(typeToName2: not done yet [%A]" x

let rec typeToCppString = function
    | UnitType -> "void"
    | Bool -> "bool"
    | Int32 -> "int"
    | StringType -> "std::string"
    | InterfaceType name -> sprintf "std::shared_ptr<%s>" name
    | ExceptionType name -> name
    | DelegateType name -> sprintf "std::function<%s>" name
    | ArrayType niType -> sprintf "std::vector<%s>" (typeToCppString niType)
    | MapType (keyType, valueType) -> sprintf "std::map<%s,%s>" (typeToCppString keyType) (typeToCppString valueType)
    | StructType name -> name
    | TypedefType name -> name
    | EnumType name -> name
    | SumType name -> name
    | x -> sprintf "(typeToCppString: not done yet [%A])" x

let rec typeToInternalKey = function
    | UnitType -> "void"
    | Bool -> "bool"
    | Int32 -> "int32"
    | StringType -> "string"
    | InterfaceType name -> name
    | ExceptionType name -> name
    | DelegateType name -> name
    | ArrayType niType -> sprintf "array[%s]" (typeToInternalKey niType)
    | MapType (keyType, valueType) -> sprintf "map[%s,%s]" (typeToInternalKey keyType) (typeToInternalKey valueType)
    | StructType name -> name
    | TypedefType name -> name
    | EnumType name -> name
    | SumType name -> name
    | x -> sprintf "(typeToInternalKey: not done yet [%A])" x

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
    | Bool -> "false"
    | Int32 -> "0"
    | StringType -> "\"default\""
    | InterfaceType name -> sprintf "std::shared_ptr<%s>()" name
    | DelegateType name -> sprintf "std::function<%s>()" name
    | ArrayType niType -> sprintf "std::vector<%s>()" (typeToCppString niType)
    | MapType (keyType, valueType) -> sprintf "std::map<%s,%s>()" (typeToCppString keyType) (typeToCppString valueType)
    | _ ->
        "(defaultValueForType: not yet supported)"

type PopValueContext =
    | Variable of name: string
    | Return

type PushIsReturn =
    | Direct of value: bool
    | Indirect of name: string
    | NotApplicable

let popValue (writer: TextWriter) (level: int) (type_: NIType) (context: PopValueContext) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    match type_ with
    | UnitType ->
        match context with
        | Return -> ()  // empty!
        | Variable _ ->
            failwithf "tried to pop a Unit value into a variable, why?"
    | Bool ->
        match context with
        | Variable name ->
            write level "auto %s = ni_popBool();" name
        | Return ->
            write level "return ni_popBool();"
    | Int32 ->
        match context with
        | Variable name ->
            write level "auto %s = ni_popInt();" name
        | Return ->
            write level "return ni_popInt();"
    | StringType ->
        match context with
        | Variable name ->
            write level "auto %s = popStringInternal();" name
        | Return ->
            write level "return popStringInternal();"
    | InterfaceType ifaceName ->
        match context with
        | Variable name ->
            write level "auto %s = %s__pop();" name ifaceName
        | Return ->
            write level "return %s__pop();" ifaceName
    | DelegateType delegateName ->
        match context with
        | Variable name ->
            write level "auto %s = %s__pop();" name delegateName
        | Return ->
            write level "return %s__pop();" delegateName
    | ArrayType niType ->
        let popFunc =
            match niType with
            | Bool -> "popBoolArrayInternal"
            | Int32 -> "popIntArrayInternal"
            | StringType -> "popStringArrayInternal"
            | StructType name -> sprintf "%sArray__pop" name
            | TypedefType name -> sprintf "%sArray__pop" name
            | DelegateType name -> sprintf "%sArray__pop" name
            | ArrayType _ -> sprintf "%sArray__pop" (typeToName niType)
            | EnumType name -> sprintf "%sArray__pop" name
            | SumType name -> sprintf "%sArray__pop" name
            | _ -> sprintf "// (pop not yet supported: %A)" type_
        match context with
        | Variable name ->
            write level "auto %s = %s();" name popFunc
        | Return ->
            write level "return %s();" popFunc
    | MapType (keyType, valueType) ->
        match context with
        | Variable name ->
            write level "auto %s = __%s%sMap__pop();" name (typeToName keyType) (typeToName valueType)
        | Return ->
            write level "return __%s%sMap__pop();"  (typeToName keyType) (typeToName valueType)
    | StructType structName ->
        match context with
        | Variable name ->
            write level "auto %s = %s__pop();" name structName
        | Return ->
            write level "return %s__pop();" structName
    | TypedefType typedefName ->
        match context with
        | Variable name ->
            write level "auto %s = %s__pop();" name typedefName
        | Return ->
            write level "return %s__pop();" typedefName
    | EnumType enumTypeName ->
        match context with
        | Variable name ->
            write level "auto %s = %s__pop();" name enumTypeName
        | Return ->
            write level "return %s__pop();" enumTypeName
    | SumType sumTypeName ->
        match context with
        | Variable name ->
            write level "auto %s = %s__pop();" name sumTypeName
        | Return ->
            write level "return %s__pop();" sumTypeName
    | _ ->
        write level "// (pop not yet implemented: %A)" type_

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
    | Bool ->
        write level "ni_pushBool(%s);" content
    | Int32 ->
        write level "ni_pushInt(%s);" content
    | StringType ->
        write level "pushStringInternal(%s);" content
    | InterfaceType name ->
        write level "%s__push(%s, %s);" name content (isReturnString())
    | DelegateType name ->
        write level "%s__push(%s);" name content // still hmm about safety area for functions ...
    | ArrayType niType ->
        match niType with
        | Bool ->
            write level "pushBoolArrayInternal(%s);" content
        | Int32 ->
            write level "pushIntArrayInternal(%s);" content
        | StringType ->
            write level "pushStringArrayInternal(%s);" content
        | ArrayType niType ->
            write level "%sArrayArray__push(%s, %s);" (typeToName niType) content (isReturnString())
        | MapType (keyType, valueType) ->
            write level "%s%sMapArray__push(%s, %s);" (typeToName keyType) (typeToName valueType) content (isReturnString())
        | StructType name ->
            write level "%sArray__push(%s, %s);" name content (isReturnString())
        | DelegateType name ->
            write level "%sArray__push(%s, %s);" name content (isReturnString()) // currently doesn't use the isReturn' value, but lets us emit array stuff more easily
        | TypedefType name ->
            write level "%sArray__push(%s, %s);" name content (isReturnString())
        | EnumType name ->
            write level "%sArray__push(%s, %s);" name content (isReturnString())
        | SumType name ->
            write level "%sArray__push(%s, %s);" name content (isReturnString())
        | _ ->
            write level "// (push not yet supported: ArrayType %A)" niType
    | MapType (keyType, valueType) ->
        write level "__%s%sMap__push(%s, %s);" (typeToName keyType) (typeToName valueType) content (isReturnString())
    | StructType name ->
        write level "%s__push(%s, %s);" name content (isReturnString())
    | TypedefType name ->
        write level "%s__push(%s, %s);" name content (isReturnString())
    | EnumType name ->
        write level "%s__push(%s);" name content
    | SumType name ->
        write level "%s__push(%s, %s);" name content (isReturnString())
    | _ ->
        write level "// (pushValue not supported yet: %A)" type_

[<RequireQualifiedAccess>]
type UserOrGeneric =
    | User of UserType
    | Generic of NIType
    | FwdInterface      // just a special case for rendering, really - won't be in the type map or anything

let userTypesAndGenerics (m: Module) =
    // combined map
    let m1 =
        (Map.empty, getUserTypes m)
        ||> Map.fold (fun newMap name userType -> newMap.Add(name, UserOrGeneric.User userType))
    (m1, allGenericTypes m)
    ||> List.fold (fun newMap type_ ->
        let name =
            (typeToInternalKey type_)
        newMap.Add(name, UserOrGeneric.Generic type_))

let createContainsRefsMap (typeMap: Map<string, UserOrGeneric>) =
    // more specifically: when pushing this type to stack, is an interface reference ever pushed in the process?
    // such that we'd need to clear the remote safety area?
    let rec processType (type_: NIType) (acc: Map<string, bool>) =
        match type_ with
        | OpaqueHandleType name -> processDeps name acc
        | StructType name -> processDeps name acc
        | InterfaceType name ->
            // never traverse into interface dependencies themselves ...
            true, acc.Add(name, true)
        | ExceptionType name -> processDeps name acc
        | DelegateType name -> processDeps name acc
        | TypedefType name -> processDeps name acc
        | EnumType name -> processDeps name acc
        | SumType name -> processDeps name acc
        | ArrayType _ ->
            let name = (typeToInternalKey type_)
            processDeps name acc
        | MapType _ ->
            let name = (typeToInternalKey type_)
            processDeps name acc
        | _ ->
            // primitive types
            false, acc
    and processDeps (name: string) (acc: Map<string, bool>) =
        if acc.ContainsKey(name) then
            acc[name], acc
        else
            let content = typeMap[name]
            let thisValue, subAcc =
                match content with
                | UserOrGeneric.FwdInterface _ ->
                    failwith "createContainsRefMap:processDeps: shouldn't ever encounter this"
                | UserOrGeneric.Generic niType ->
                    match niType with
                    | ArrayType niType ->
                        processType niType acc
                    | MapType (keyType, valueType) ->
                        let v1, acc2 =
                            processType keyType acc
                        let v2, acc3 =
                            processType valueType acc2
                        (v1 || v2), acc3
                    | _ ->
                        failwith "typesWithContainsRefs: unsupported generic type"
                | UserOrGeneric.User userType ->
                    match userType with
                    | OpaqueHandleDef ->
                        // never
                        false, acc
                    | StructDef namedFields ->
                        // possibly
                        let subValues, nextAcc =
                            (([], acc), namedFields)
                            ||> List.fold (fun (values, acc) field ->
                                let value, acc =
                                    processType field.Type_ acc
                                value :: values, acc)
                        let result =
                            subValues
                            |> List.exists id
                        result, nextAcc
                    | ExceptionDef namedFields ->
                        // possibly (though exceptions are never 'returned' per se, so that's another thing to keep track of when popping exception values!)
                        let subValues, nextAcc =
                            (([], acc), namedFields)
                            ||> List.fold (fun (values, acc) field ->
                                let value, acc =
                                    processType field.Type_ acc
                                value :: values, acc)
                        let result =
                            subValues
                            |> List.exists id
                        result, nextAcc
                    | InterfaceDef namedFunctions ->
                        // guaranteed 'yes' result by definition, but still run through deps to fill out acc map
                        let nextAcc =
                            (acc, namedFunctions)
                            ||> List.fold (fun acc namedFunc ->
                                let _, acc =
                                    processType namedFunc.Returns acc
                                let acc =
                                    (acc, namedFunc.Args)
                                    ||> List.fold (fun acc (_, argType) ->
                                        let _, acc =
                                            processType argType acc
                                        acc)
                                let acc =
                                    match namedFunc.Throws with
                                    | Some etype ->
                                        let _, acc =
                                            processType etype acc
                                        acc
                                    | None ->
                                        acc
                                acc)
                        true, nextAcc
                    | DelegateDef info ->
                        // never, but run through deps as above
                        let nextAcc =
                            let _, acc =
                                processType info.Returns acc
                            let acc =
                                (acc, info.Args)
                                ||> List.fold (fun acc (_, argType) ->
                                    let _, acc =
                                        processType argType acc
                                    acc)
                            // no 'throws' yet
                            acc
                        false, nextAcc
                    | TypeDef niType ->
                        // pass through
                        processType niType acc
                    | EnumDef _ ->
                        // never
                        false, acc
                    | SumTypeDef sumValues ->
                        // potentially
                        let subValues, nextAcc =
                            (([], acc), sumValues)
                            ||> List.fold (fun (values, acc) stv ->
                                let argValues, acc =
                                    (([], acc), stv.Args)
                                    ||> List.fold (fun (values, acc) (_, argType) ->
                                        let v, acc =
                                            processType argType acc
                                        v :: values, acc)
                                let sumValueContains =
                                    argValues
                                    |> List.exists id
                                sumValueContains :: values, acc)
                        let value =
                            subValues
                            |> List.exists id
                        value, nextAcc
            thisValue, subAcc.Add(name, thisValue)
    let allKeys =
        typeMap
        |> Map.toList
        |> List.map fst
    (Map.empty, allKeys)
    ||> List.fold (fun acc key ->
        let _, nextAcc =
            processDeps key acc
        nextAcc)

let renderUserTypes (writer: TextWriter) (typeMap: Map<string, UserOrGeneric>) (renderFunc: TextWriter -> string -> UserOrGeneric -> Map<string, UserOrGeneric> -> unit) =
    let rec renderType (type_: NIType) (alreadyRendered: Set<string>) =
        match type_ with
        | OpaqueHandleType name -> renderDeps name alreadyRendered
        | StructType name -> renderDeps name alreadyRendered
        | InterfaceType name ->
            // special handling for interface dependencies! they need to emit forward references,
            // depending on context (either "class <whatever>" in the header, or push/pop prototypes in the wrapper)
            let checkName =
                // just need a way of making sure we haven't done this already - no other infrastructure for fwd declarations (yet?)
                sprintf "fwd:class %s" name
            if not (alreadyRendered.Contains(checkName)) then
                renderFunc writer name UserOrGeneric.FwdInterface typeMap
                alreadyRendered.Add(checkName)
            else
                alreadyRendered
        | ExceptionType name -> renderDeps name alreadyRendered
        | DelegateType name -> renderDeps name alreadyRendered
        | TypedefType name -> renderDeps name alreadyRendered
        | EnumType name -> renderDeps name alreadyRendered
        | SumType name -> renderDeps name alreadyRendered
        | ArrayType _ ->
            let name = (typeToInternalKey type_)
            renderDeps name alreadyRendered
        | MapType _ ->
            let name = (typeToInternalKey type_)
            renderDeps name alreadyRendered
        | _ ->
            alreadyRendered
    and renderDeps (name: string) (alreadyRendered: Set<string>) =
        if alreadyRendered.Contains(name) then
            alreadyRendered
        else
            let content = typeMap.[name]
            let subRendered =
                match content with
                | UserOrGeneric.FwdInterface ->
                    failwith "renderUserTypes: shouldn't see this"
                | UserOrGeneric.Generic gType ->
                    match gType with
                    | ArrayType niType ->
                        // process each struct field as an array as well, since that's how we push/pop them
                        match niType with
                        | StructType structName ->
                            // array of struct implies array of each struct field
                            match typeMap.[structName] with
                            | UserOrGeneric.User (StructDef namedFields) ->
                                (alreadyRendered, namedFields)
                                ||> List.fold (fun already2 field -> renderType (ArrayType field.Type_) already2)
                            | _ ->
                                failwithf "renderDeps: struct (from array) didn't exist in typemap??"
                        | _ ->
                            alreadyRendered
                        // and the type itself, of course ...
                        |> renderType niType
                    | MapType (keyType, valueType) ->
                        alreadyRendered
                        |> renderType (ArrayType keyType) // these imply the keyType/valueType alone
                        |> renderType (ArrayType valueType)
                    | _ ->
                        failwithf "renderUserTypes.renderDeps: unsupported generic type %A!" gType
                | UserOrGeneric.User userType ->
                    match userType with
                    | OpaqueHandleDef -> // no sub fields
                        alreadyRendered
                    | StructDef namedFields ->
                        (alreadyRendered, namedFields)
                        ||> List.fold (fun already2 field -> renderType field.Type_ already2)
                    | ExceptionDef namedFields ->
                        (alreadyRendered, namedFields)
                        ||> List.fold (fun already2 field -> renderType field.Type_ already2)
                    | InterfaceDef namedFunctions ->
                        (alreadyRendered, namedFunctions)
                        ||> List.fold (fun already2 namedFunc ->
                            let withReturn =
                                renderType namedFunc.Returns already2
                            let withThrow =
                                match namedFunc.Throws with
                                | Some etype ->
                                    renderType etype withReturn
                                | None ->
                                    withReturn
                            (withThrow, namedFunc.Args)
                            ||> List.fold (fun already3 (_, argType) ->
                                renderType argType already3))
                    | DelegateDef info ->
                        let withReturn =
                            renderType info.Returns alreadyRendered
                        (withReturn, info.Args)
                        ||> List.fold (fun already2 (_, argType) ->
                            renderType argType already2)
                    | TypeDef actual ->
                        renderType actual alreadyRendered
                    | EnumDef _ ->
                        alreadyRendered
                    | SumTypeDef enumValues ->
                        (alreadyRendered, enumValues)
                        ||> List.fold (fun already2 value ->
                            (already2, value.Args)
                            ||> List.fold (fun already3 (_, argType) ->
                                renderType argType already3))

            // use plugin render func, since it depends on our context
            renderFunc writer name content typeMap

            // done
            subRendered.Add(name)
    // process all keys (names) - renderDeps just skips anything it's seen before
    let keys =
        typeMap
        |> Map.toList
        |> List.map fst
    (Set.empty, keys)
    ||> List.fold (fun alreadyRendered k -> renderDeps k alreadyRendered)
    |> ignore

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
    pushValue writer 2 info.Returns invocation (Direct true)
    write 1 "};"
    write 1 "pushServerFuncVal(wrapper, uniqueKey);"
    write 0 "}"
    // pop
    blank()
    write 0 "std::function<%s> %s__pop() {" name name
    write 1 "auto id = ni_popClientFunc();"
    write 1 "auto cf = std::shared_ptr<ClientFunc>(new ClientFunc(id));"
    write 1 "auto wrapper = [cf](%s) {" (joinedArgs info.Args)
    for argName, argType in info.Args |> List.rev do
        pushValue writer 2 argType argName (Direct false)
    write 2 "cf->remoteExec();"
    if typeContainsRefs info.Returns then
        popValue writer 2 info.Returns (Variable "__ret")
        write 2 "ni_clearClientSafetyArea();"
        write 2 "return __ret;"
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
        match namedFunc.Throws with
        | Some _ ->
            write 2 "invokeMethodWithExceptions(%s_%s);" (lowerFirst name) namedFunc.Name
        | None ->
            write 2 "invokeMethod(%s_%s);" (lowerFirst name) namedFunc.Name
        // pop result
        if typeContainsRefs namedFunc.Returns then
            // any sort of reference that might get freed if it was only a return value from the other side (potentially stored nowhere else, so protected by the safety area)
            // this is to prevent premature deref which could free the instance before we've gotten ahold of it
            popValue writer 2 namedFunc.Returns (Variable "__ret")
            write 2 "ni_clearClientSafetyArea();"
            write 2 "return __ret;"
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
        pushValue writer 1 namedField.Type_ memberVar (Direct false) // false = not a concern because it still exists in this class instance
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
    pushValue writer 1 Int32 "(int)value.tag" (Indirect "isReturn")
    write 0 "}"
    // pop
    blank()
    write 0 "%s %s__pop() {" sumTypeName sumTypeName
    popValue writer 1 Int32 (Variable "which")
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

let renderWrapperUserTypes (containsRefsMap: Map<string,bool>) (writer: TextWriter) (name: string) (content: UserOrGeneric) (typeMap: Map<string, UserOrGeneric>) =
    let write (level: int) (fmt: FSharp.Core.Printf.TextWriterFormat<'T>) =
        writer.Write(String.replicate level "    ")
        fprintfn writer fmt
    let blank () = write 0 ""
    let typeContainsRefs (type_: NIType) =
        containsRefsMap.ContainsKey(typeToInternalKey type_)
    blank()
    match content with
    | UserOrGeneric.FwdInterface ->
        write 0 "void %s__push(std::shared_ptr<%s> thing, bool isReturn);" name name
        write 0 "std::shared_ptr<%s> %s__pop();" name name
    | UserOrGeneric.Generic gType ->
        match gType with
        | MapType (keyType, valueType) ->
            // PUSH =====
            write 0 "void __%s__push(%s _map, bool isReturn) {" (typeToCppString gType) (typeToCppString gType)
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
            write 0 "%s __%s__pop() {" (typeToCppString gType) (typeToCppString gType)
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
                // use this for any types with no particular specializations (typedefs, delegates, arrays of arrays -- everything but structs, basically)
                // PUSH ========
                write 0 "void %sArray__push(std::vector<%s> values, bool isReturn) {" name (typeToCppString elementType)
                write 1 "for (auto v = values.rbegin(); v != values.rend(); v++) {" // in reverse!
                pushValue writer 2 elementType "*v" (Indirect "isReturn")
                write 1 "}"
                write 1 "ni_pushSizeT(values.size());"
                write 0 "}"
                // POP =========
                blank()
                write 0 "std::vector<%s> %sArray__pop() {" (typeToCppString elementType) name
                write 1 "std::vector<%s> __ret;" (typeToCppString elementType)
                write 1 "auto count = ni_popSizeT();"
                write 1 "for (auto i = 0; i < count; i++) {"
                popValue writer 2 elementType (Variable "value")
                write 2 "__ret.push_back(value);"
                write 1 "}"
                write 1 "return __ret;"
                write 0 "}"
            match arrayType with
            | StructType structName ->
                // fetch the fields so we can make a struct-of-arrays
                match typeMap.[structName] with
                | UserOrGeneric.User (StructDef namedFields) ->
                    // PUSH ===========
                    write 0 "void %sArray__push(%s values, bool isReturn) {" structName (typeToCppString gType)
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
                    write 0 "%s %sArray__pop() {" (typeToCppString gType) structName
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
                arrayCommon name arrayType
            | DelegateType name ->
                arrayCommon name arrayType
            | ArrayType _ ->
                arrayCommon (typeToName arrayType) arrayType
            | EnumType name ->
                arrayCommon name arrayType
            | SumType name ->
                arrayCommon name arrayType
            | _ ->
                // we don't handle any of the primitives, those are all built-in
                write 0 "// push/pop for generic %s" (typeToCppString gType)
        | _ ->
            write 0 "// push/pop for generic %s" (typeToCppString gType)
    | UserOrGeneric.User userType ->
        match userType with
        | OpaqueHandleDef ->
            write 0 "// anything for opaque here? push/pop perhaps? %s" name
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
            pushValue writer 1 Int32 "(int)value" NotApplicable
            write 0 "}"
            blank()
            write 0 "inline %s %s__pop() {" name name
            popValue writer 1 Int32 (Variable "tag")
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
        renderWrapperUserTypes containsRefsMap
    renderUserTypes writer typeMap renderFunc

    // plain function wrappers
    for namedFunction in moduleMethods do
        blank()
        write 0 "void %s__wrapper() {" namedFunction.Name
        for argName, argType in namedFunction.Args do
            popValue writer 1 argType (Variable argName)
        let invocation =
            sprintf "%s(%s)" namedFunction.Name (joinedArgNames namedFunction.Args)
        match namedFunction.Throws with
        | Some (ExceptionType exceptionName) ->
            write 1 "try {"
            pushValue writer 2 namedFunction.Returns invocation (Direct true)
            write 1 "}"
            write 1 "catch (const %s& e) {" exceptionName
            write 2 "ni_setException(%s);" (lowerFirst exceptionName)
            write 2 "%s__push(e);" exceptionName
            write 1 "}"
        | _ -> // note we're not handling Some (non-exception types)! need to be errored out on type checking ...
            pushValue writer 1 namedFunction.Returns invocation (Direct true)
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
            match nf.Throws with
            | Some (ExceptionType exceptionName) ->
                write 1 "try {"
                pushValue writer 2 nf.Returns invocation (Direct true)
                write 1 "}"
                write 1 "catch (const %s& e) {" exceptionName
                write 2 "ni_setException(%s);" (lowerFirst exceptionName)
                write 2 "%s__push(e);" exceptionName
                write 1 "}"
            | _ -> // note we're not handling Some (non-exception types)! need to be errored out on type checking ...
                pushValue writer 1 nf.Returns invocation (Direct true)
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
            match namedFunc.Throws with
            | Some exceptionType -> sprintf "  // throws %s" (typeToCppString exceptionType)
            | None -> ""
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

let renderHeaderUserTypes (writer: TextWriter) (name: string) (content: UserOrGeneric) (typeMap: Map<string, UserOrGeneric>) =
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
        | _ ->
            failwithf "renderUserTypes: unsupported generic type %A" gType
    | UserOrGeneric.User userType ->
        match userType with
        | OpaqueHandleDef ->
            write 0 "struct __%s; typedef struct __%s* %s;" name name name
        | StructDef namedFields ->
            write 0 "typedef struct {"
            for field in namedFields do
                write 1 "%s %s;" (typeToCppString field.Type_) field.Name
            write 0 "} %s;" name
        | ExceptionDef namedFields ->
            renderHeaderException writer name namedFields
        | InterfaceDef namedFunctions ->
            renderHeaderInterface writer name namedFunctions
        | DelegateDef info ->
            write 0 "typedef %s %s(%s);" (typeToCppString info.Returns) name (joinedArgs info.Args)
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

    // all user types in dependency order
    renderUserTypes writer (userTypesAndGenerics m) renderHeaderUserTypes

    // global externs
    if globals.Length > 0 then
        blank()
        for globalName, globalType in globals do
            write 0 "extern const %s %s;" (typeToCppString globalType) globalName

    // function prototypes
    blank()
    for namedFunc in moduleMethods do
        let throwsComment =
            match namedFunc.Throws with
            | Some exceptionType -> sprintf "  // throws %s" (typeToCppString exceptionType)
            | None -> ""
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
    // functions
    for namedFunc in moduleMethods do
        blank()
        let throwsComment =
            match namedFunc.Throws with
            | Some exceptionType -> sprintf "  // throws %s" (typeToCppString exceptionType)
            | None -> ""
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
