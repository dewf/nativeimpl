module Generators.Common

open System
open System.IO
open Parser
open Extensions
open AppCommon
open Util

let joinNameParts (separator: string) (parts: string list) =
    String.Join(separator, parts)

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
            joinNameParts "." nameParts
        | Imported(ModuleName moduleNameParts, SymbolName nameParts) ->
            joinNameParts "." (moduleNameParts @ nameParts)
    | ArrayType niType -> sprintf "array[%s]" (typeToInternalKey niType)
    | MapType (keyType, valueType) -> sprintf "map[%s,%s]" (typeToInternalKey keyType) (typeToInternalKey valueType)
    | BufferType niType -> sprintf "buffer[%s]" (typeToInternalKey niType)
    | TupleType niTypes -> sprintf "tuple[%s]" (niTypes |> List.map typeToInternalKey |> String.concat ",")
    | SetType niType -> sprintf "set[%s]" (typeToInternalKey niType)
    | OptionType niType -> sprintf "option[%s]" (typeToInternalKey niType)
    | ResultType (successType, errorType) -> sprintf "result[%s,%s]" (typeToInternalKey successType) (typeToInternalKey errorType)

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
            // TODO: synthesized generics for optimizations for other types, eg Option<T> ?
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
            t :: (k @ v @ ks @ vs)
        | ArrayType niType ->
            t :: arrayOrSetHandling niType
        | BufferType _ ->
            [t] // buffers only support primitive elements, no subresults
        | TupleType niTypes ->
            t :: (niTypes |> List.collect typeAndSubTypes)
        | SetType niType ->
            t :: arrayOrSetHandling niType
        | OptionType elem ->
            t :: typeAndSubTypes elem
        | ResultType (succ, err) ->
            t :: (typeAndSubTypes succ @ typeAndSubTypes err)
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
                    let nestedTypes =
                        match info.MaybeStaticDefs with
                        | Some staticDefs ->
                            processDefs staticDefs
                        | None ->
                            []
                    let methodTypes =
                        namedFuncsHandler info.Methods
                    methodTypes @ nestedTypes
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
    // is this sufficient/correct for dependency order? (we want smaller primitives to be rendered before the composites they comprise)
    |> List.rev
    |> List.distinct

type RenderableEntry =
    | RenderableDirect of thing: UserOrGeneric
    | RenderableContainer of sub: RenderableTable
    | RenderableHybrid of thing: UserOrGeneric * sub: RenderableTable
and RenderableTable =
    Map<string, RenderableEntry>
    
let rec userToRenderable (symbolTable: SymbolTable): RenderableTable =
    symbolTable
    |> Map.map (fun _ entry ->
        match entry with
        | Direct thing ->
            RenderableDirect (UserOrGeneric.User thing)
        | SubTable table ->
            let sub =
                userToRenderable table
            RenderableContainer sub
        | Hybrid (thing, table) ->
            let sub =
                userToRenderable table
            RenderableHybrid (UserOrGeneric.User thing, sub))
    
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
        | OptionType elem ->
            processType elem
        | ResultType (succ, err) ->
            processType succ || processType err
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
        | EnumDef _ ->
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
        | Some _bRemain ->
            // b contained all of a and then some (well, technically bRemain could be empty if a == b, but the first branch above removes that possibility)
            NSDescendsFrom
        | None ->
            match List.removePrefix b a with
            | Some _aRemain ->
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
        | Some (key :: _) ->
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

type IArrayLikeRenderer =
    abstract TypedefTuple: NIType -> unit
    abstract EnumType: name: ResolvedName -> unit
    abstract StructType: name: ResolvedName -> NamedField list -> unit
    abstract TupleType: NIType list -> unit
    abstract ArrayType: unit -> unit
    abstract MapType: unit -> unit
    abstract DelegateType: unit -> unit
    abstract SetType: unit -> unit
    abstract OptionType: NIType -> unit
    abstract ResultType: NIType -> NIType -> unit
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
    | SetType _ ->
        renderer.SetType ()
    | OptionType elemType ->
        renderer.OptionType elemType
    | ResultType (succType, errType) ->
        renderer.ResultType succType errType
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

type GroupContext = {
    Name: string
    Path: string list
    Defs: ModuleDefinition list
    // broken down for easy use:
    ModuleMethods: NamedFunction list
    Opaques: (string * OpaqueInfo) list
    Interfaces: (string * NamedFunction list) list
    Exceptions: (string * NamedField list) list
    Globals: (string * NIType) list
    SubOrder: string list                
    SubGroups: Map<string, GroupContext> // will be rendered in definition order, interleaved with user types, so better to have a name-keyed Map ... but we also store order for when we are dealing only with groups
}
type RenderContext = {
    ModuleContext: ModuleContext
    GroupContext: GroupContext
}

let createGroups (rootDefs: ModuleDefinition list) =
    let rec recInner (name: string) (groupDefs: ModuleDefinition list) (revPath: string list) =
        let moduleMethods =
            groupDefs
            |> List.choose (function
                | FunctionDef namedFunction -> Some namedFunction
                | _ -> None)
        let opaques =
            groupDefs
            |> List.choose (function
                | UserType (name, OpaqueDef info) ->
                    Some (name, info)
                | _ ->
                    None)
        let interfaces =
            groupDefs
            |> List.choose (function
                | UserType(name, InterfaceDef namedFunctions) -> Some (name, namedFunctions)
                | _ -> None)
        let exceptions =
            groupDefs
            |> List.choose (function
                | UserType(name, ExceptionDef fields) -> Some (name, fields)
                | _ -> None)
        let globals =
            groupDefs
            |> List.choose (function
                | Global(name, niType) -> Some (name, niType)
                | _ -> None)
        let subOrder, subGroups =
            let pairs = 
                groupDefs
                |> List.choose (function
                    | Group(name, defs) ->
                        let group =
                            recInner name defs (name :: revPath)
                        Some (name, group)
                    | UserType (name, OpaqueDef info) ->
                        info.MaybeStaticDefs
                        |> Option.map (fun defs ->
                            let group =
                                recInner name defs (name :: revPath)
                            name, group)
                    | _ -> None)
            let order =
                pairs
                |> List.map fst
            order, pairs |> Map.ofList
        { Name = name
          Path = List.rev revPath
          Defs = groupDefs
          ModuleMethods = moduleMethods
          Opaques = opaques
          Interfaces = interfaces
          Exceptions = exceptions
          Globals = globals
          SubOrder = subOrder
          SubGroups = subGroups }
    recInner "$ROOT$" rootDefs []
    
let createRenderContext (moduleContext: ModuleContext) =
    let root =
        createGroups moduleContext.Module.Defs
    { ModuleContext = moduleContext; GroupContext = root }
    
type ResolveNameStringMode =
    | TypeMode
    | IdentifierMode
    | GenericIdentifierMode
let resolvedNameToStringAbstract (languageNsDelim: string) (context: RenderContext) (name: ResolvedName) (mode: ResolveNameStringMode) =
    let separator =
        match mode with
        | TypeMode -> languageNsDelim
        | IdentifierMode | GenericIdentifierMode -> "_"
    match name with
    | ThisModule (SymbolName parts) ->
        // strip current namespace prefix if possible
        // (but not in Generics mode)
        let parts2 =
            match mode with
            | GenericIdentifierMode -> parts
            | _ ->
                match List.removePrefix context.GroupContext.Path parts with
                | Some remaining -> remaining
                | None -> parts
        joinNameParts separator (parts2 |> List.map upperFirst)
    | Imported(moduleName, symbolName) ->
        let symbolParts =
            match symbolName with
            | SymbolName parts -> parts
        if symbolIsUnambiguous context.ModuleContext symbolName then
            joinNameParts separator (symbolParts |> List.map upperFirst)
        else
            match moduleName with
            | ModuleName moduleParts ->
                joinNameParts separator ((moduleParts @ symbolParts) |> List.map upperFirst)
    // match mode with
    // | TypeMode ->
    //     // type mode behaves a bit differently, we want to remove the current namespace prefix if possible for ThisModule names
    //     match name with
    //     | ThisModule (SymbolName parts) ->
    //         // strip current namespace prefix if possible
    //         let parts2 =
    //             match List.removePrefix context.GroupContext.Path parts with
    //             | Some remaining -> remaining
    //             | None -> parts
    //         joinNameParts separator (parts2 |> List.map upperFirst)
    //     | Imported(moduleName, symbolName) ->
    //         let symbolParts =
    //             match symbolName with
    //             | SymbolName parts -> parts
    //         if symbolIsUnambiguous context.ModuleContext symbolName then
    //             joinNameParts separator (symbolParts |> List.map upperFirst)
    //         else
    //             match moduleName with
    //             | ModuleName moduleParts ->
    //                 joinNameParts separator ((moduleParts @ symbolParts) |> List.map upperFirst)
    // | IdentifierMode ->
    //     match name with
    //     | ThisModule (SymbolName parts) ->
    //         // strip current namespace prefix if possible
    //         let parts2 =
    //             match List.removePrefix context.GroupContext.Path parts with
    //             | Some remaining -> remaining
    //             | None -> parts
    //         joinNameParts separator (parts2 |> List.map upperFirst)
    //     | Imported(moduleName, symbolName) ->
    //         let symbolParts =
    //             match symbolName with
    //             | SymbolName parts -> parts
    //         if symbolIsUnambiguous context.ModuleContext symbolName then
    //             joinNameParts separator (symbolParts |> List.map upperFirst)
    //         else
    //             match moduleName with
    //             | ModuleName moduleParts ->
    //                 joinNameParts separator ((moduleParts @ symbolParts) |> List.map upperFirst)
    //     // // in identifier mode, we just use the full symbol name prefix unless imported and ambiguous,
    //     // // then use full module + symbol name
    //     // let moduleName, symbolName =
    //     //     match name with
    //     //     | ThisModule symbolName ->
    //     //         context.ModuleContext.Module.Name, symbolName
    //     //     | Imported(moduleName, symbolName) ->
    //     //         moduleName, symbolName
    //     // match symbolName with
    //     // | SymbolName symbolParts ->
    //     //     // we want to use the shortest UNAMBIGUOUS name
    //     //     // if symbolName alone yields multiple results (in this module or imports), use the fully qualified name
    //     //     if symbolIsUnambiguous context.ModuleContext symbolName then
    //     //         // DON'T strip the common namespace in identifier mode, we need the full thing for eg generics push/pop
    //     //         joinNameParts separator (symbolParts |> List.map upperFirst)
    //     //     else
    //     //         match moduleName with
    //     //         | ModuleName moduleParts ->
    //     //             joinNameParts separator ((moduleParts @ symbolParts) |> List.map upperFirst)
