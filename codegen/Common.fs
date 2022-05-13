module Common

open System.Collections.Generic
open Parser

let rec typeToInternalKey = function
    | UnitType -> "void"
    | Primitive prim ->
        match prim with
        | SizeT -> "size_t"
        | Bool -> "bool"
        | Int(signed, width) ->
            match signed, width with
            | true, Width8 -> "int8"
            | false, Width8 -> "uint8"
            | true, Width16 -> "int16"
            | false, Width16 -> "uint16"
            | true, Width32 -> "int32"
            | false, Width32 -> "uint32"
            | true, Width64 -> "int64"
            | false, Width64 -> "uint64"
        | Float32 -> "float32"
        | Float64 -> "float64"
        | StringType -> "string"
    | Unresolved _ -> failwith "typeToInternalKey: Unresolved??"
    | OpaqueHandleType name -> name
    | InterfaceType name -> name
    | ExceptionType name -> name
    | DelegateType name -> name
    | ArrayType niType -> sprintf "array[%s]" (typeToInternalKey niType)
    | MapType (keyType, valueType) -> sprintf "map[%s,%s]" (typeToInternalKey keyType) (typeToInternalKey valueType)
    | BufferType niType -> sprintf "buffer[%s]" (typeToInternalKey niType)
    | TupleType niTypes -> sprintf "tuple[%s]" (niTypes |> List.map typeToInternalKey |> String.concat ",")
    | SetType niType -> sprintf "set[%s]" (typeToInternalKey niType)
    | StructType name -> name
    | TypedefType name -> name
    | EnumType name -> name
    | SumType name -> name

[<RequireQualifiedAccess>]
type UserOrGeneric =
    | User of UserType
    | Generic of NIType
    | FwdInterface      // just a special case for rendering, really - won't be in the type map or anything
    
let allGenericTypes (module_: Module) (userTypeMap: Map<string, UserType>) =
    let rec typeAndSubTypes (t: NIType) =
        let tupleHandling (tupleOuter: NIType) =
            match tupleOuter with
            | TupleType tupleTypes ->
                let sub =
                    tupleTypes
                    |> List.collect (fun type_ -> typeAndSubTypes (ArrayType type_))
                // include the tuple itself!
                tupleOuter :: sub
            | _ ->
                failwith "!!!"
        let arrayOrSetHandling (niType: NIType) =
            // special handling for array/set of struct/tuple (anything with fields): we render arrays of each field/subtype
            match niType with
            | StructType name ->
                match userTypeMap[name] with
                | StructDef namedFields ->
                    namedFields
                    |> List.collect (fun field -> typeAndSubTypes (ArrayType field.Type_))
                | _ ->
                    failwith "allGenericTypes/thingWithFieldsSubResults: some issue getting struct def for array of struct"
            | TupleType _ ->
                tupleHandling niType
            | TypedefType name ->
                // array of typedefs of tuple need to be flattened, otherwise we won't have the necessary synthetic array types in our lookup tables
                match userTypeMap[name] with
                | TypeDef (TupleType _ as tt) ->
                    // need to also return the collapsed array type - the caller of arrayOrSetHandling will still have (ArrayType (TypeDef...))
                    (ArrayType tt) :: tupleHandling tt
                | _ ->
                    // any other typedef type, ignore (currently)
                    []
            | _ ->
                // any other array type, no special handling
                typeAndSubTypes niType
        // ========================================================================
        match t with
        | MapType(keyType, valueType) ->
            // implies arrays of key/value, necessary for push/pop of maps
            // wouldn't just the arrays (ks/vs) be sufficient?
            let k = typeAndSubTypes keyType
            let v = typeAndSubTypes valueType
            let ks = typeAndSubTypes (ArrayType keyType)
            let vs = typeAndSubTypes (ArrayType valueType)
            t :: k @ v @ ks @ vs
        | ArrayType niType ->
            t :: arrayOrSetHandling niType
        | BufferType _ ->
            [t] // buffers only support primitive elements, no subresults
        | TupleType niTypes ->
            t :: (niTypes |> List.collect typeAndSubTypes)
        | SetType niType ->
            t :: arrayOrSetHandling niType
        | _ -> []
    module_.Defs
    |> List.collect (function
        | UserType(_, userType) ->
            match userType with
            | OpaqueHandleDef -> []
            | StructDef namedFields ->
                namedFields
                |> List.collect (fun field -> typeAndSubTypes field.Type_)
            | ExceptionDef namedFields ->
                namedFields
                |> List.collect (fun field -> typeAndSubTypes field.Type_)
            | InterfaceDef namedFunctions ->
                namedFunctions
                |> List.collect (fun nf ->
                    let retTypes =
                        typeAndSubTypes nf.Returns
                    let argTypes =
                        nf.Args
                        |> List.map snd
                        |> List.collect typeAndSubTypes
                    let throwTypes =
                        nf.Throws
                        |> List.collect typeAndSubTypes
                    retTypes @ argTypes @ throwTypes)
            | DelegateDef info ->
                let retTypes =
                    typeAndSubTypes info.Returns
                let throwTypes =
                    info.Throws
                    |> List.collect typeAndSubTypes
                let argTypes =
                    info.Args
                    |> List.map snd
                    |> List.collect typeAndSubTypes
                retTypes @ throwTypes @ argTypes
            | TypeDef actual ->
                typeAndSubTypes actual
            | EnumDef _ ->
                []
            | SumTypeDef values ->
                values
                |> List.collect (fun value ->
                    value.Args
                    |> List.map snd
                    |> List.collect typeAndSubTypes)
        | Global (_, niType) ->
            typeAndSubTypes niType
        | FunctionDef nf ->
            let retTypes =
                typeAndSubTypes nf.Returns
            let argTypes =
                nf.Args
                |> List.map snd
                |> List.collect typeAndSubTypes
            let throwTypes =
                nf.Throws
                |> List.collect typeAndSubTypes
            retTypes @ argTypes @ throwTypes)
    |> List.distinct

let userTypesAndGenerics (m: Module) =
    // combined map
    let userTypes =
        getUserTypes m
    let m1 =
        (Map.empty, userTypes)
        ||> Map.fold (fun newMap name userType -> newMap.Add(name, UserOrGeneric.User userType))
    let allGenerics =
        allGenericTypes m userTypes
//    printfn "all generics: %A" allGenerics
    (m1, allGenerics)
    ||> List.fold (fun newMap type_ ->
        let name =
            (typeToInternalKey type_)
        newMap.Add(name, UserOrGeneric.Generic type_))

let createContainsRefsMap (typeMap: Map<string, UserOrGeneric>) =
    // more specifically: when pushing this type to stack, is an interface reference ever pushed in the process?
    // such that we'd need to clear the remote safety area?
    let rec processType (type_: NIType) (acc: Map<string, bool>) =
        match type_ with
        | UnitType ->
            false, acc
        | Primitive _ ->
            false, acc
        | OpaqueHandleType name ->
            false, acc.Add(name, false)
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
        | TupleType _ ->
            let name = (typeToInternalKey type_)
            processDeps name acc
        | SetType _ ->
            let name = (typeToInternalKey type_)
            processDeps name acc
        | BufferType _ ->
            // buffers only contain primitives (so no processDeps), but are themselves reference types (that require clearing remote safety area when returned)
            let name = (typeToInternalKey type_)
            true, acc.Add(name, true)
        | Unresolved _ ->
            failwith "createContainsRefsMap/processType encountered 'Unresolved' type??"
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
                    | TupleType niTypes ->
                        let subValues, nextAcc =
                            (([], acc), niTypes)
                            ||> List.fold (fun (values, acc) type_ ->
                                let value, acc =
                                    processType type_ acc
                                value :: values, acc)
                        let result =
                            subValues
                            |> List.exists id
                        result, nextAcc
                    | SetType niType ->
                        processType niType acc
                    | BufferType _ ->
                        // always
                        true, acc
                    | _ ->
                        failwithf "createContainsRefsMap: unsupported generic type (%A)" niType
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
                                    (acc, namedFunc.Throws)
                                    ||> List.fold (fun acc eType ->
                                        let _, acc =
                                            processType eType acc
                                        acc)
                                acc)
                        true, nextAcc
                    | DelegateDef info ->
                        // never, but run through deps as above
                        let nextAcc =
                            let _, acc =
                                processType info.Returns acc
                            let acc =
                                (acc, info.Throws)
                                ||> List.fold (fun acc eType ->
                                    let _, acc =
                                        processType eType acc
                                    acc)
                            let acc =
                                (acc, info.Args)
                                ||> List.fold (fun acc (_, argType) ->
                                    let _, acc =
                                        processType argType acc
                                    acc)
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

let renderUserTypes (typeMap: Map<string, UserOrGeneric>) (renderFunc: string -> UserOrGeneric -> Map<string, UserOrGeneric> -> unit) =
    let rec renderType (type_: NIType) (alreadyRendered: Set<string>) =
        match type_ with
        | UnitType ->
            alreadyRendered
        | Primitive _ ->
            alreadyRendered
        | OpaqueHandleType name -> renderDeps name alreadyRendered
        | StructType name -> renderDeps name alreadyRendered
        | InterfaceType name ->
            // special handling for interface dependencies! they need to emit forward references,
            // depending on context (either "class <whatever>" in the header, or push/pop prototypes in the wrapper)
            let checkName =
                // just need a way of making sure we haven't done this already - no other infrastructure for fwd declarations (yet?)
                sprintf "fwd:class %s" name
            if not (alreadyRendered.Contains(checkName)) then
                renderFunc name UserOrGeneric.FwdInterface typeMap
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
        | TupleType _ ->
            let name = (typeToInternalKey type_)
            renderDeps name alreadyRendered
        | SetType _ ->
            let name = (typeToInternalKey type_)
            renderDeps name alreadyRendered
        | BufferType _ ->
            let name = (typeToInternalKey type_)
            renderDeps name alreadyRendered
        | Unresolved _ ->
            failwith "renderUserTypes/renderType: 'Unresolved' type encountered??"
    and renderDeps (name: string) (alreadyRendered: Set<string>) =
        if alreadyRendered.Contains(name) then
            alreadyRendered
        else
            let content =
                try
                    typeMap[name]
                with
                | :? KeyNotFoundException ->
                    printfn "contents of typemap: %A" typeMap
                    failwithf "key %s not found" name
            let subRendered =
                match content with
                | UserOrGeneric.FwdInterface ->
                    failwith "renderUserTypes: shouldn't see this"
                | UserOrGeneric.Generic gType ->
                    // common stuff for array/set
                    let arrayOrSetHandling (niType: NIType) (alreadyRendered: Set<string>) =
                        // process each struct field as an array as well, since that's how we push/pop them
                        match niType with
                        | StructType structName ->
                            // array of struct implies array of each struct field
                            match typeMap[structName] with
                            | UserOrGeneric.User (StructDef namedFields) ->
                                (alreadyRendered, namedFields)
                                ||> List.fold (fun already2 field -> renderType (ArrayType field.Type_) already2)
                            | _ ->
                                failwithf "renderDeps: struct (from array) didn't exist in typemap??"
                        | TupleType tupleTypes ->
                            // array of tuple implies array of each tuple subtype
                            (alreadyRendered, tupleTypes)
                            ||> List.fold (fun already2 type_ -> renderType (ArrayType type_) already2)
                        | TypedefType typeDefName ->
                            // arrays of typedefs of tuple are flattened
                            // (why penalize the user because they wanted a typedef?)
                            // (this just creates the dependency on the array-of-<tuplefield>s, so that the requisite array push/pop methods exist)
                            match typeMap[typeDefName] with
                            | UserOrGeneric.User (TypeDef (TupleType tupleTypes as tt)) ->
                                (alreadyRendered, tupleTypes)
                                ||> List.fold (fun already2 type_ -> renderType (ArrayType type_) already2)
                                // + the tuple type itself, sigh
                                |> renderType (ArrayType tt)
                            | _ ->
                                alreadyRendered
                        | _ ->
                            alreadyRendered
                        // and the type itself, of course ...
                        |> renderType niType
                    // begin generic type switch ===================================================================
                    match gType with
                    | ArrayType niType ->
                        arrayOrSetHandling niType alreadyRendered
                    | MapType (keyType, valueType) ->
                        // maps are pushed/popped as keys+values arrays
                        alreadyRendered
                        |> renderType (ArrayType keyType) // these imply the keyType/valueType alone
                        |> renderType (ArrayType valueType)
                    | TupleType tupleTypes ->
                        (alreadyRendered, tupleTypes)
                        ||> List.fold (fun already2 type_ -> renderType type_ already2)
                    | SetType niType ->
                        arrayOrSetHandling niType alreadyRendered
                    | BufferType elemType ->
                        // could probably omit this since it's always a primitive type ...
                        alreadyRendered
                        |> renderType elemType
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
                            let withThrows =
                                (withReturn, namedFunc.Throws)
                                ||> List.fold (fun already3 eType ->
                                    renderType eType already3)
                            (withThrows, namedFunc.Args)
                            ||> List.fold (fun already3 (_, argType) ->
                                renderType argType already3))
                    | DelegateDef info ->
                        let withReturn =
                            renderType info.Returns alreadyRendered
                        let withThrows =
                            (withReturn, info.Throws)
                            ||> List.fold (fun already3 eType ->
                                renderType eType already3)
                        (withThrows, info.Args)
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
            renderFunc name content typeMap

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

type IArrayLikeRenderer =
    abstract TypedefTuple: NIType -> unit
    abstract EnumType: name: string -> unit
    abstract StructType: name: string -> NamedField list -> unit
    abstract TupleType: NIType list -> unit
    abstract ArrayType: unit -> unit
    abstract MapType: unit -> unit
    abstract DelegateType: unit -> unit
    abstract SetType: unit -> unit
    abstract SumType: unit -> unit
    abstract Primitive: NIPrimitive -> unit
    abstract Other: NIType -> unit

let renderArrayLike (typeMap: Map<string, UserOrGeneric>) (elemType: NIType) (renderer: IArrayLikeRenderer) =
    match elemType with
    | TypedefType typeDefName ->
        match typeMap[typeDefName] with
        | UserOrGeneric.User (TypeDef (TupleType _ as tt)) ->
            renderer.TypedefTuple tt
        | _ ->
            failwithf "array of typedef %A: TODO (basic array treatment)" typeDefName
    | EnumType name ->
        renderer.EnumType name
    | StructType structName ->
        // fetch the fields so we can make a struct-of-arrays
        match typeMap[structName] with
        | UserOrGeneric.User (StructDef namedFields) ->
            renderer.StructType structName namedFields
        | _ ->
            failwithf "renderWrapperUserOrGeneric: failed to find struct in typemap"
    | TupleType tupleTypes ->
        renderer.TupleType tupleTypes
    | ArrayType _ ->
        renderer.ArrayType ()
    | MapType _ ->
        renderer.MapType ()
    | Primitive prim ->
        renderer.Primitive prim
    | other ->
        renderer.Other other
