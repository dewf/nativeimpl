module Generators.Common

open System
open System.IO
open Parser
open Extensions
open AppCommon

let joinNameParts (parts: string list) =
    String.Join(".", parts)

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
    | Resolved (name, _) ->
        match name with
        | ThisModule (SymbolName nameParts) ->
            joinNameParts nameParts
        | Imported(ModuleName moduleNameParts, SymbolName nameParts) ->
            joinNameParts (moduleNameParts @ nameParts)
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
    
let requiredSymbolLookup (context: ModuleContext) (name: ResolvedName) =
    match name with
    | ThisModule (SymbolName nameParts) ->
        let table =
            match context.Module.SymbolTable with
            | Complete table -> table
            | _ -> failwith "requiredSymbolLookup: ThisModule table not complete"
        match findSymbolHere table nameParts with
        | Some value ->
            value
        | None ->
            failwith "requiredSymbolLookup: findSymbolHere failed (for ThisModule)"
    | Imported (importName, SymbolName nameParts) ->
        match context.Loaded.TryFind importName with
        | Some m ->
            let table =
                match m.SymbolTable with
                | Complete table -> table
                | _ -> failwith "requiredSymbolLookup: imported module symbol table not complete"
            match findSymbolHere table nameParts with
            | Some value ->
                value
            | None ->
                failwith "requiredSymbolLookup: findSymbolHere failed (for imported symbol)"
        | None -> failwith "todo"
        
let allGenericTypes (context: ModuleContext) =
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
            | Resolved (resolvedName, resolvedType) ->
                match resolvedType with
                | StructType ->
                    match requiredSymbolLookup context resolvedName with
                    | StructDef namedFields ->
                        namedFields
                        |> List.collect (fun field -> typeAndSubTypes (ArrayType field.Type_))
                    | _ ->
                        failwith "allGenericTypes:typeAndSubTypes:arrayOrSetHandling: some issue getting struct def for array of struct"
                | TypedefType ->
                    // array of typedefs of tuple need to be flattened, otherwise we won't have the necessary synthetic array types in our lookup tables
                    match requiredSymbolLookup context resolvedName with
                    | TypeDef (TupleType _ as tt) ->
                        // need to also return the collapsed array type - the caller of arrayOrSetHandling will still have (ArrayType (TypeDef...))
                        (ArrayType tt) :: tupleHandling tt
                    | _ ->
                        // any other typedef type, ignore (currently)
                        []
                | _ ->
                    // any other named type, no special handling (yet)
                    typeAndSubTypes niType
            | TupleType _ ->
                tupleHandling niType
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
    let rec processDefs (defs: ModuleDefinition list) =
        defs
        |> List.collect (function
            | Group (_, groupDefs) ->
                processDefs groupDefs
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
                    namedFuncsHandler info.Methods
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
    // go
    processDefs context.Module.Defs
    |> List.distinct

type RenderableEntry =
    | RenderableDirect of thing: UserOrGeneric
    | RenderableContainer of sub: RenderableTable
and RenderableTable =
    Map<string, RenderableEntry>
    
let rec userToRenderable (symbolTable: SymbolTable): RenderableTable =
    symbolTable
    |> Map.map (fun k entry ->
        match entry with
        | Direct thing ->
            RenderableDirect (UserOrGeneric.User thing)
        | SubTable table ->
            let sub =
                userToRenderable table
            RenderableContainer sub)
    
let userTypesAndGenerics (context: ModuleContext) =
    // create combined map
    let userTypes =
        match context.Module.SymbolTable with
        | Complete table -> table
        | _ ->
            failwithf "userTypesAndGenerics (module %A) - symbol table not 'Complete'" context.Module.Name
    let map1 =
        userToRenderable userTypes
    let allGenerics =
        allGenericTypes context
    (map1, allGenerics)
    ||> List.fold (fun newMap type_ ->
        let name =
            (typeToInternalKey type_)
        newMap.Add(name, RenderableDirect (UserOrGeneric.Generic type_)))
    
let containsRefs (context: ModuleContext) (thing: NIType) =
    let rec processType (t: NIType) =
        match t with
        | UnitType -> false
        | Primitive _ -> false
        | ArrayType elem ->
            processType elem
        | MapType(keyType, valueType) ->
            processType keyType || processType valueType
        | TupleType tupleTypes ->
            List.exists processType tupleTypes
        | SetType elem ->
            processType elem
        | BufferType _ ->
            // elemType can only be primitive
            false
        | Unresolved _ ->
            failwith "containsRefs:processType - Unresolved?? wut"
        | Resolved(resolvedName, _) ->
            let userType =
                requiredSymbolLookup context resolvedName
            processUserType userType
    and processUserType (userType: UserType) =
        match userType with
        | OpaqueDef _ ->
            false
        | StructDef fields ->
            fields
            |> List.exists (fun nf -> processType nf.Type_)
        | ExceptionDef fields ->
            // hard to imagine this carrying an interface, but I guess we haven't forbidden it?
            fields
            |> List.exists (fun f -> processType f.Type_)
        | InterfaceDef _ ->
            true
        | DelegateDef _ ->
            false
        | TypeDef actual ->
            processType actual
        | EnumDef info ->
            false
        | SumTypeDef values ->
            values
            |> List.exists (fun v ->
                v.Args
                |> List.exists (fun (_, argType) -> processType argType))
        | OptionsDef fields ->
            fields
            |> List.exists (fun f -> processType f.Type_)
    processType thing
    
type NamespaceRelation =
    | NSEqual
    | NSDescendsFrom   // B descends from A
    | NSAncestorOf     // B ancestor of A
    | NSUnrelated      // parallel / alien (they'll have a common ancestor somewhere, if only at root)
    
let getNamespaceRelation (a: string list) (b: string list) =
    // how does B relate to A?
    if a = b then
        NSEqual
    else
        match List.removePrefix a b with
        | Some bRemain ->
            // b contained all of a and then some (well, technically bRemain could be empty if a == b, but the first branch above removes that possibility)
            NSDescendsFrom
        | None ->
            match List.removePrefix b a with
            | Some aRemain ->
                // a contained all of b and then some (again, couldn't be equal because of initial 'if')
                NSAncestorOf
            | None ->
                // neither was a prefix of the other
                NSUnrelated
    
let symbolToRenderableKey (SymbolName nameParts) (currentNamespace: string list) =
    let key, symbolNamespace =
        match nameParts |> List.rev with
        | key :: nsRev ->
            key, nsRev |> List.rev
        | [] ->
            failwith "symbolToRenderableKey: nameParts was empty??"
    match getNamespaceRelation currentNamespace symbolNamespace with
    | NSEqual ->
        // symbol exists in current NS, OK to render directly
        Some key
    | NSDescendsFrom ->
        // symbol is in a namespace nested from current, truncate down and grab whatever is in this NS (eg opaque container at this level)
        match List.removePrefix currentNamespace symbolNamespace with
        | Some (key :: etc) ->
            Some key
        | Some [] ->
            failwith "symbolToRenderableKey: removed common namespace, but nothing left!"
        | None ->
            failwith "symbolToRenderableKey: failed to remove common namespace"
    | NSAncestorOf ->
        // symbol is at a prior NS level, ignore
        None
    | NSUnrelated ->
        // parallel / unrelated namespace with an unknown common ancestor, ignore
        None

let renderUserTypes (currentNamespace: string list) (table: RenderableTable) (renderFunc: string -> UserOrGeneric -> RenderableTable -> unit) =
    let rec renderType (type_: NIType) (alreadyRendered: Set<string>) =
        match type_ with
        | UnitType ->
            alreadyRendered
        | Primitive _ ->
            alreadyRendered
        | Resolved (resolvedName, resolvedType) ->
            match resolvedName with
            | Imported _ ->
                // don't attempt to render external stuff!
                alreadyRendered
            | ThisModule symbolName ->
                // compare against current namespace, and use that to decide whether to render the symbol (or its container) or what
                // 1. if equal, OK to proceed
                // 2. if symbol NS is child of current, truncate to this level (eg opaque static item -> opaque itself)
                // 3. if symbol NS is parent of current, ignore
                // 4. if symbol NS is unrelated NS, ignore
                // TODO ** don't forget!! when computing the dependencies of an opaque item, we must consider the nested contents of its static block. but I think this Resolved() match branch will filter things nicely (to only whatever is relevant to the current namespace / render level)
                match symbolToRenderableKey symbolName currentNamespace with
                | None ->
                    // not our problem!
                    alreadyRendered
                | Some name ->
                    match resolvedType with
                    | InterfaceType ->
                        // TODO: try to understand this again, have completely forgotten the exact reasoning
                        // something about avoiding mutually-dependent interfaces I think, which would lead to infinite render loops
                        // and since the things would exist in the root table coming in to renderUserTypes, they'll definitely get rendered (since we iterate over the table keys to begin with)
                        // for the InterfaceType, it will render a fwd declaration (if the target language requires such a thing)
                        // but then we're not doing that for opaques?
                        // I guess we could spit out all of them at the tops of the files entirely, vs. as-needed-by-dependency like this
                        
                        // special handling for interface dependencies! they need to emit forward references,
                        // depending on context (either "class <whatever>" in the header, or push/pop prototypes in the wrapper)
                        let checkName =
                            // just need a way of making sure we haven't done this already - no other infrastructure for fwd declarations (yet?)
                            sprintf "fwd:class %s" name
                        if not (alreadyRendered.Contains(checkName)) then
                            renderFunc name UserOrGeneric.FwdInterface table
                            alreadyRendered.Add(checkName)
                        else
                            alreadyRendered
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

    and renderModuleDef (moduleDefinition: ModuleDefinition) (alreadyRendered: Set<string>) =
        match moduleDefinition with
        | Group (name, groupDefs) ->
            (alreadyRendered, groupDefs)
            ||> List.fold (fun acc def ->
                renderModuleDef def acc)
        | Global(_, globalType) ->
            renderType globalType alreadyRendered
        | FunctionDef namedFunc ->
            let withReturn =
                renderType namedFunc.Returns alreadyRendered
            let withThrows =
                (withReturn, namedFunc.Throws)
                ||> List.fold (fun already3 eType ->
                    renderType eType already3)
            (withThrows, namedFunc.Args)
            ||> List.fold (fun already3 (_, argType) ->
                renderType argType already3)
        | UserType(_, userType) ->
            renderUserTypeDeps userType alreadyRendered
    
    and renderUserTypeDeps (userType: UserType) (alreadyRendered: Set<string>) =
        match userType with
        | OpaqueDef info ->
            (alreadyRendered, info.Methods)
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
        
    and renderDeps (name: string) (alreadyRendered: Set<string>) =
        if alreadyRendered.Contains(name) then
            alreadyRendered
        else
            let content =
                match table.TryFind name with
                | Some entry ->
                    match entry with
                    | RenderableDirect thing -> thing
                    | RenderableContainer sub ->
                        failwith "renderDeps table.TryFind: name matched thing-less container"
                | None ->
                    failwith "renderDeps table.TryFind: no entry found"
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
                            match resolvedName with
                            | Imported(moduleName, name) ->
                                failwithf "renderDeps:arrayOrSetHandling (%A|%A) - how should we handle imported types?" moduleName name
                            | ThisModule symbolName ->
                                // something from our own module, OK
                                // but is it in the current namespace/scope?
                                match symbolToRenderableKey symbolName currentNamespace with
                                | None ->
                                    // uhh, not sure if this is sensible
                                    alreadyRendered
                                | Some name ->
                                    // really just behaving analogously to other parts of this code
                                    // don't know if this makes sense in this context ...
                                    // ultimately we're just trying to render dependencies, right?
                                    let content2 =
                                        match table.TryFind name with
                                        | Some entry ->
                                            match entry with
                                            | RenderableDirect thing ->
                                                thing
                                            | RenderableContainer sub ->
                                                failwith "renderDeps arrayOrSetHandling table.TryFind: name matched container"
                                        | None ->
                                            failwith "renderDeps arrayOrSetHandling table.TryFind: failed to get content of Resolved type"
                                    match resolvedType with
                                    | StructType ->
                                        // array of struct implies array of each struct field
                                        match content2 with
                                        | UserOrGeneric.User (StructDef namedFields) ->
                                            (alreadyRendered, namedFields)
                                            ||> List.fold (fun already2 field -> renderType (ArrayType field.Type_) already2)
                                        | _ ->
                                            failwithf "renderDeps: struct (from array) didn't exist in typemap??"
                                    | TypedefType ->
                                        // arrays of typedefs of tuple are flattened
                                        // (why penalize the user because they wanted a typedef?)
                                        // (this just creates the dependency on the array-of-<tuplefield>s, so that the requisite array push/pop methods exist)
                                        match content2 with
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
                    renderUserTypeDeps userType alreadyRendered

            // use plugin render func, since it depends on our context
            renderFunc name content table

            // done
            subRendered.Add(name)
    // process all keys (names) - renderDeps just skips anything it's seen before
    let keys =
        table
        |> Map.toList
        |> List.map fst
    (Set.empty, keys)
    ||> List.fold (fun alreadyRendered k -> renderDeps k alreadyRendered)
    |> ignore

type IArrayLikeRenderer =
    abstract TypedefTuple: NIType -> unit
    abstract EnumType: name: ResolvedName -> unit
    abstract StructType: name: ResolvedName -> NamedField list -> unit
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

let renderArrayLike (context: ModuleContext) (elemType: NIType) (renderer: IArrayLikeRenderer) =
    match elemType with
    | Resolved(resolvedName, resolvedType) ->
        let content =
            requiredSymbolLookup context resolvedName
        match resolvedType with
        | TypedefType ->
            match content with
            | TypeDef (TupleType _ as tt) ->
                renderer.TypedefTuple tt
            | _ ->
                failwithf "array of typedef %A: TODO (basic array treatment)" resolvedName
        | EnumType ->
            renderer.EnumType resolvedName
        | StructType ->
            // fetch the fields so we can make a struct-of-arrays
            match content with
            | StructDef namedFields ->
                renderer.StructType resolvedName namedFields
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
    abstract WithExtraIndent : int -> IFormatWriter

type SimpleFormatWriter(writer: TextWriter, baseIndent: int) =
    interface IDisposable with
        member this.Dispose() =
            writer.Dispose()
    interface IFormatWriter with
        member this.Write indent fmt =
            writer.Write(String.replicate (baseIndent + indent) "    ")
            fprintfn writer fmt
        member this.Blank() =
            writer.WriteLine()
        member this.WithExtraIndent value =
            new SimpleFormatWriter(writer, baseIndent + value)

let namespaceSplitter (namespace': string) =
    let parts =
        namespace'.Split(".")
    let pre, post =
        parts
        |> Array.splitAt (parts.Length - 1)
    (pre |> String.concat "."), post[0]
    
let maybeJoinedPrefix (parts: string list) =
    match parts with
    | _ :: _ ->
        String.Join(".", parts)
        |> Some
    | _ ->
        None
        
let namespaceParts (outParams: OutParams) (ModuleName nameParts) =
    let baseNamespace =
        outParams.MaybeNamespace
        |> Option.map (fun ns -> sprintf "%s.%s" ns outParams.LibraryName)
        |> Option.defaultValue outParams.LibraryName
    let modulePrefix, className =
        List.splitAtLast nameParts
    baseNamespace, maybeJoinedPrefix modulePrefix, className
