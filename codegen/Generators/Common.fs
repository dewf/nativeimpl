module Generators.Common

open System
open System.Collections.Generic
open System.IO
open Parser
open Util

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
    | Resolved (resolvedName, _) ->
        resolvedName.Flatten()
    | ArrayType niType -> sprintf "array[%s]" (typeToInternalKey niType)
    | MapType (keyType, valueType) -> sprintf "map[%s,%s]" (typeToInternalKey keyType) (typeToInternalKey valueType)
    | BufferType niType -> sprintf "buffer[%s]" (typeToInternalKey niType)
    | TupleType niTypes -> sprintf "tuple[%s]" (niTypes |> List.map typeToInternalKey |> String.concat ",")
    | SetType niType -> sprintf "set[%s]" (typeToInternalKey niType)

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
            | Resolved (resolvedName, StructType) ->
                let name =
                    resolvedName.Flatten()
                match userTypeMap[name] with
                | StructDef namedFields ->
                    namedFields
                    |> List.collect (fun field -> typeAndSubTypes (ArrayType field.Type_))
                | _ ->
                    failwith "allGenericTypes/thingWithFieldsSubResults: some issue getting struct def for array of struct"
            | TupleType _ ->
                tupleHandling niType
            | Resolved (resolvedName, TypedefType) ->
                let name =
                    resolvedName.Flatten()
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
            let namedFuncsHandler (namedFunctions: NamedFunction list) =
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
            match userType with
            | OpaqueDef info ->
                let staticMethods =
                    info.StaticDefs
                    |> List.choose (function
                        | FunctionDef nf -> Some nf
                        | _ -> None)
                namedFuncsHandler (info.Methods @ staticMethods)
            | StructDef namedFields ->
                namedFields
                |> List.collect (fun field -> typeAndSubTypes field.Type_)
            | OptionsDef namedFields ->
                namedFields
                |> List.collect (fun field -> typeAndSubTypes field.Type_)
            | ExceptionDef namedFields ->
                namedFields
                |> List.collect (fun field -> typeAndSubTypes field.Type_)
            | InterfaceDef namedFunctions ->
                namedFuncsHandler namedFunctions
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
    // create combined map
    let userTypes =
        m.SymbolTable.Value
    let map1 =
        (Map.empty, userTypes)
        ||> Map.fold (fun newMap name userType -> newMap.Add(name, UserOrGeneric.User userType))
    let allGenerics =
        allGenericTypes m userTypes
    (map1, allGenerics)
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
        | Resolved (resolvedName, resolvedType) ->
            let name =
                resolvedName.Flatten()
            match resolvedType with
            | OpaqueType ->
                false, acc.Add(name, false)
            | StructType ->
                processDeps name acc
            | InterfaceType ->
                // never traverse into interface dependencies themselves ...
                true, acc.Add(name, true)
            | ExceptionType ->
                processDeps name acc
            | DelegateType ->
                processDeps name acc
            | TypedefType ->
                processDeps name acc
            | EnumType ->
                processDeps name acc
            | SumType ->
                processDeps name acc
            | OptionsType ->
                processDeps name acc
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
                | UserOrGeneric.FwdInterface ->
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
                    | OpaqueDef info ->
                        // guaranteed 'no' result by definition, but still run through deps to fill out acc map
                        let nextAcc =
                            (acc, info.Methods)
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
                        false, nextAcc
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
                    | OptionsDef namedFields ->
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
    
    
let rec renderUserTypesV2 (typeMap: Map<string, UserOrGeneric>) (renderFunc: string -> UserOrGeneric -> Map<string, UserOrGeneric> -> unit) =
    // process all keys (names) - renderDeps just skips anything it's seen before
    let keys =
        typeMap
        |> Map.toList
        |> List.map fst
    let depsFunc (id: string) =
        // return id list of dependencies
        let typeList =
            match typeMap[id] with
            | UserOrGeneric.User userType ->
                match userType with
                | OpaqueDef info ->
                    info.Methods
                    |> List.collect (fun nf ->
                        let argTypes =
                            nf.Args
                            |> List.map snd
                        nf.Returns :: argTypes @ nf.Throws)
                | StructDef fields ->
                    fields
                    |> List.map (_.Type_)
                | ExceptionDef fields ->
                    fields
                    |> List.map (_.Type_)
                | InterfaceDef methods ->
                    methods
                    |> List.collect (fun nf ->
                        let argTypes =
                            nf.Args
                            |> List.map snd
                        nf.Returns :: argTypes @ nf.Throws)
                | DelegateDef info ->
                    let argTypes =
                        info.Args
                        |> List.map snd
                    info.Returns :: argTypes @ info.Throws
                | TypeDef actual ->
                    [ actual ]
                | EnumDef info ->
                    info.MaybeWidth
                    |> Option.toList
                | SumTypeDef values ->
                    values
                    |> List.collect (_.Args >> List.map snd)
                | OptionsDef fields ->
                    fields
                    |> List.map (_.Type_)
            | UserOrGeneric.Generic niType ->
                match niType with
                | ArrayType elem ->
                    // todo: special array handling for certain types
                    //   eg array of struct, implies struct of field arrays
                    [ elem ]
                | MapType(keyType, valueType) ->
                    // implies array of keytype, array of valuetype etc
                    [ keyType; valueType ]
                | TupleType tupleTypes ->
                    tupleTypes
                | SetType elem ->
                    [ elem ]
                | BufferType elemType ->
                    [ elemType ]
                | _ ->
                    failwithf "renderUserTypesV2: unsupported generic type: %A" niType
            | UserOrGeneric.FwdInterface ->
                failwith "blah not using these?"
        // from raw types to (user-defined) keys
        typeList
        |> List.map typeToInternalKey
        // we only want the user-defined types provided to this function (no need for primitives etc)
        |> List.filter typeMap.ContainsKey
    let ordered =
        DepsResolver.depsOrderSolver keys id depsFunc
    // TODO: render ordered list!
    ordered
        

let renderUserTypes (typeMap: Map<string, UserOrGeneric>) (renderFunc: string -> UserOrGeneric -> Map<string, UserOrGeneric> -> unit) =
    let rec renderType (type_: NIType) (alreadyRendered: Set<string>) =
        match type_ with
        | UnitType ->
            alreadyRendered
        | Primitive _ ->
            alreadyRendered
        | Resolved (resolvedName, resolvedType) ->
            match resolvedName with
            | Imported(path, name) ->
                // don't attempt to render external stuff!
                alreadyRendered
            | ThisModule name ->
                match resolvedType with
                | OpaqueType ->
                    // special handling for opaque dependencies! see interface notes below
                    let checkName =
                        // just need a way of making sure we haven't done this already - no other infrastructure for fwd declarations (yet?)
                        sprintf "fwd:opaque %s" name
                    if not (alreadyRendered.Contains(checkName)) then
                        // don't emit anything at all for these
                        alreadyRendered.Add(checkName)
                    else
                        alreadyRendered
                | StructType ->
                    renderDeps name alreadyRendered
                | InterfaceType ->
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
                | ExceptionType ->
                    renderDeps name alreadyRendered
                | DelegateType ->
                    renderDeps name alreadyRendered
                | TypedefType ->
                    renderDeps name alreadyRendered
                | EnumType ->
                    renderDeps name alreadyRendered
                | SumType ->
                    renderDeps name alreadyRendered
                | OptionsType ->
                    renderDeps name alreadyRendered
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
                    // printfn "contents of typemap: %A" typeMap
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
                        | Resolved(resolvedName, resolvedType) ->
                            let name =
                                resolvedName.Flatten()
                            match resolvedType with
                            | StructType ->
                                // array of struct implies array of each struct field
                                match typeMap[name] with
                                | UserOrGeneric.User (StructDef namedFields) ->
                                    (alreadyRendered, namedFields)
                                    ||> List.fold (fun already2 field -> renderType (ArrayType field.Type_) already2)
                                | _ ->
                                    failwithf "renderDeps: struct (from array) didn't exist in typemap??"
                            | TypedefType ->
                                // arrays of typedefs of tuple are flattened
                                // (why penalize the user because they wanted a typedef?)
                                // (this just creates the dependency on the array-of-<tuplefield>s, so that the requisite array push/pop methods exist)
                                match typeMap[name] with
                                | UserOrGeneric.User (TypeDef (TupleType tupleTypes as tt)) ->
                                    (alreadyRendered, tupleTypes)
                                    ||> List.fold (fun already2 type_ -> renderType (ArrayType type_) already2)
                                    // + the tuple type itself, sigh
                                    |> renderType (ArrayType tt)
                                | _ ->
                                    alreadyRendered
                            | _ ->
                                alreadyRendered
                        | TupleType tupleTypes ->
                            // array of tuple implies array of each tuple subtype
                            (alreadyRendered, tupleTypes)
                            ||> List.fold (fun already2 type_ -> renderType (ArrayType type_) already2)
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
                    | OpaqueDef info ->
                        // static methods
                        // but how can we namespace-prefix these? and other static defs?
                        let staticMethods =
                            info.StaticDefs
                            |> List.choose (function
                                | FunctionDef nf -> Some nf
                                | _ -> None)
                        // static + instance method processing
                        (alreadyRendered, info.Methods @ staticMethods)
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
                    | StructDef namedFields ->
                        (alreadyRendered, namedFields)
                        ||> List.fold (fun already2 field -> renderType field.Type_ already2)
                    | OptionsDef namedFields ->
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
    abstract Opaque: unit -> unit
    abstract Options: unit -> unit
    abstract Other: NIType -> unit

let renderArrayLike (typeMap: Map<string, UserOrGeneric>) (elemType: NIType) (renderer: IArrayLikeRenderer) =
    match elemType with
    | Resolved(resolvedName, resolvedType) ->
        let name =
            resolvedName.Flatten()
        match resolvedType with
        | TypedefType ->
            match typeMap[name] with
            | UserOrGeneric.User (TypeDef (TupleType _ as tt)) ->
                renderer.TypedefTuple tt
            | _ ->
                failwithf "array of typedef %A: TODO (basic array treatment)" name
        | EnumType ->
            renderer.EnumType name
        | StructType ->
            // fetch the fields so we can make a struct-of-arrays
            match typeMap[name] with
            | UserOrGeneric.User (StructDef namedFields) ->
                renderer.StructType name namedFields
            | _ ->
                failwithf "renderWrapperUserOrGeneric: failed to find struct in typemap"
        | SumType ->
            renderer.SumType ()
        | OpaqueType ->
            renderer.Opaque ()
        | OptionsType ->
            renderer.Options ()
        | _ ->
            renderer.Other elemType
    | TupleType tupleTypes ->
        renderer.TupleType tupleTypes
    | ArrayType _ ->
        renderer.ArrayType ()
    | MapType _ ->
        renderer.MapType ()
    | Primitive prim ->
        renderer.Primitive prim
    | _ ->
        renderer.Other elemType

// workaround for not being able to pass functions which take format strings
// (formerly necessitated fully declaring 'write' and 'blank' anew in every function)
type IFormatWriter =
    abstract Write : int -> FSharp.Core.Printf.TextWriterFormat<'T> -> 'T
    abstract Blank : unit -> unit

type SimpleFormatWriter(writer: TextWriter) =
    interface IDisposable with
        member this.Dispose() =
            writer.Dispose()
    interface IFormatWriter with
        member this.Write indent fmt =
            writer.Write(String.replicate indent "    ")
            fprintfn writer fmt
        member this.Blank() =
            writer.WriteLine()

type OutParams = {
    Path: string
    MaybeNamespace: string option
    LibraryName: string
}

let namespaceSplitter (namespace': string) =
    let parts =
        namespace'.Split(".")
    let pre, post =
        parts
        |> Array.splitAt (parts.Length - 1)
    (pre |> String.concat "."), post[0]
    
let namespaceParts (outParams: OutParams) (moduleName: string) =
    let baseNamespace =
        outParams.MaybeNamespace
        |> Option.map (fun ns -> sprintf "%s.%s" ns outParams.LibraryName)
        |> Option.defaultValue outParams.LibraryName
    let modulePrefix, className =
        moduleName
        |> namespaceSplitter
    baseNamespace, maybeString modulePrefix, className
