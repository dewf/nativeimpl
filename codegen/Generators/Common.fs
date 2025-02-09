module Generators.Common

open System
open System.IO
open ConstParser
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

// convert all first-level "ThisModule" symbols to imported, from a particular modulename
// this is necessary when we're operating on sub-elements of things we've imported
// eg array handling of imported structs
// right now this only operates on things with sub-fields
let makeFirstLevelImported (toName: ModuleName) (ut: UserType) =
    let nextType (t: NIType) =
        match t with
        | Resolved (resolvedName, resolvedType) ->
            let nextName =
                match resolvedName with
                | ThisModule name ->
                    Imported (toName, name)
                | _ ->
                    resolvedName
            Resolved (nextName, resolvedType)
        | _ ->
            t
    let nextNamedField (nf: NamedField) =
        { nf with Type_ = nextType nf.Type_ }
    match ut with
    | StructDef info ->
        let nextFields =
            info.Fields
            |> List.map nextNamedField
        // do we need to go into the static defs? to sleepy to think about it ...
        StructDef { info with Fields = nextFields }
    | ExceptionDef fields ->
        let nextFields =
            fields
            |> List.map nextNamedField
        ExceptionDef nextFields
    | SumTypeDef values ->
        let nextValues =
            values
            |> List.map (fun stv ->
                let nextArgs =
                    stv.Args
                    |> List.map (fun (name, type_) -> name, nextType type_)
                { stv with Args = nextArgs })
        SumTypeDef nextValues
    | OptionsDef fields ->
        let nextFields =
            fields
            |> List.map nextNamedField
        OptionsDef nextFields
    | _ ->
        // unchanged
        ut

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
            failwithf "requiredSymbolLookup: findSymbolHere failed (for ThisModule) - name: %A" name
    | Imported (importName, SymbolName nameParts) ->
        match context.Loaded.TryFind importName with
        | Some m ->
            let table =
                match m.SymbolTable with
                | Complete table -> table
                | _ -> failwith "requiredSymbolLookup: imported module symbol table not complete"
            match findSymbolHere table nameParts with
            | Some value ->
                // value
                // !!
                // we need to recursively convert all ThisModule resolved names, Imported instead!
                // because when we were looking up imported symbols for eg. array rendering, we were incorrecntly seeing them as located in ThisModule
                makeFirstLevelImported importName value
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
                    | StructDef info ->
                        info.Fields
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
        let namedFuncHandler (nf: NamedFunction) =
            let retTypes =
                typeAndSubTypes nf.Returns
            let argTypes =
                nf.Args
                |> List.map (_.Type)
                |> List.collect typeAndSubTypes
            let throwTypes =
                nf.Throws
                |> List.collect typeAndSubTypes
            retTypes @ argTypes @ throwTypes
        let namedFuncsHandler (namedFunctions: NamedFunction list) =
            namedFunctions
            |> List.collect namedFuncHandler
        defs
        |> List.collect (function
            | Group (_, groupDefs) ->
                processDefs groupDefs
            | UserType(_, userType) ->
                match userType with
                | FwdOpaqueDef _ ->
                    // no nested types
                    []
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
                | StructDef info ->
                    let nestedTypes =
                        match info.MaybeStaticDefs with
                        | Some staticDefs ->
                            processDefs staticDefs
                        | None ->
                            []
                    let fieldTypes =
                        info.Fields
                        |> List.collect (fun field -> typeAndSubTypes field.Type_)
                    fieldTypes @ nestedTypes
                | OptionsDef namedFields ->
                    namedFields
                    |> List.collect (fun field -> typeAndSubTypes field.Type_)
                | ExceptionDef namedFields ->
                    namedFields
                    |> List.collect (fun field -> typeAndSubTypes field.Type_)
                | InterfaceDef info ->
                    let nestedTypes =
                        match info.MaybeStaticDefs with
                        | Some staticDefs ->
                            processDefs staticDefs
                        | None ->
                            []
                    let methodTypes =
                        namedFuncsHandler info.Methods
                    methodTypes @ nestedTypes
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
                | NamedBitsDef _ ->
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
                namedFuncHandler nf )
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
    // does pushing this type push any ref types in the process?
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
        | FwdOpaqueDef _ ->
            false
        | OpaqueDef _ ->
            false
        | StructDef info ->
            info.Fields
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
        | NamedBitsDef _ ->
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
    abstract TypedefPrim: NIPrimitive -> unit
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
            | TypeDef (Primitive prim) ->
                renderer.TypedefPrim prim
            | _ ->
                failwithf "array of typedef %A: TODO (basic array treatment)" resolvedName
        | EnumType ->
            renderer.EnumType resolvedName
        | StructType ->
            // fetch the fields so we can make a struct-of-arrays
            match content with
            | StructDef info ->
                renderer.StructType resolvedName info.Fields
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
    
type GroupKind =
    | NormalGroup
    | OpaqueStatic
    | InterfaceStatic
    | StructStatic

type GroupContext = {
    Name: string
    Kind: GroupKind
    Path: string list
    KindPath: GroupKind list
    Defs: ModuleDefinition list
    // broken down for easy use:
    ModuleMethods: NamedFunction list
    Opaques: (string * OpaqueInfo) list
    FwdOpaques: (string * string list) list
    SumTypes: (string * SumTypeValue list) list
    Interfaces: (string * InterfaceInfo) list
    Exceptions: (string * NamedField list) list
    Globals: (string * NIType) list
    SubOrder: string list                
    SubGroups: Map<string, GroupContext> // will be rendered in definition order, interleaved with user types, so better to have a name-keyed Map ... but we also store order for when we are dealing only with groups
}

type RenderContext = {
    ModuleContext: ModuleContext
    GroupContext: GroupContext
} with
    member this.NavigateToSub(name: string) =
        let nextGroup =
            this.GroupContext.SubGroups[name]
        { this with GroupContext = nextGroup }

let createGroups (rootDefs: ModuleDefinition list) =
    let rec recInner (name: string) (kind: GroupKind) (groupDefs: ModuleDefinition list) (revPath: (string * GroupKind) list) =
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
        let fwdOpaques =
            groupDefs
            |> List.choose (function
                | UserType (name, FwdOpaqueDef parts) ->
                    Some (name, parts)
                | _ ->
                    None)
        let sumTypes =
            groupDefs
            |> List.choose (function
                | UserType (name, SumTypeDef values) ->
                    Some (name, values)
                | _ ->
                    None)
        let interfaces =
            groupDefs
            |> List.choose (function
                | UserType(name, InterfaceDef info) ->
                    Some (name, info)
                | _ ->
                    None)
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
                            let subKind =
                                NormalGroup
                            let nextRevPath =
                                (name, subKind) :: revPath
                            recInner name subKind defs nextRevPath
                        Some (name, group)
                    | UserType (name, userType) ->
                        let processNested (subKind: GroupKind) (defs: ModuleDefinition list) =
                            let group =
                                let nextRevPath =
                                    (name, subKind) :: revPath
                                recInner name subKind defs nextRevPath
                            name, group
                        match userType with
                        | OpaqueDef info ->
                            info.MaybeStaticDefs
                            |> Option.map (processNested OpaqueStatic)
                        | InterfaceDef info ->
                            info.MaybeStaticDefs
                            |> Option.map (processNested InterfaceStatic)
                        | StructDef info ->
                            info.MaybeStaticDefs
                            |> Option.map (processNested StructStatic)
                        | _ ->
                            None
                    | _ ->
                        None)
            let order =
                pairs
                |> List.map fst
            order, pairs |> Map.ofList
        let path, kindPath =
            List.rev revPath
            |> List.unzip
        { Name = name
          Kind = kind
          Path = path
          KindPath = kindPath
          Defs = groupDefs
          ModuleMethods = moduleMethods
          Opaques = opaques
          FwdOpaques = fwdOpaques
          SumTypes = sumTypes
          Interfaces = interfaces
          Exceptions = exceptions
          Globals = globals
          SubOrder = subOrder
          SubGroups = subGroups }
    recInner "$ROOT$" NormalGroup rootDefs []
    
let createRenderContext (moduleContext: ModuleContext) =
    let root =
        createGroups moduleContext.Module.Defs
    { ModuleContext = moduleContext; GroupContext = root }
    
let resolvedNamePathToKindPath (m: ModuleContext) (name: ResolvedName) =
    let m, parts = 
        match name with
        | ThisModule (SymbolName parts) ->
            m.Module, parts
        | Imported(moduleName, SymbolName parts) ->
            m.Loaded[moduleName], parts
    let rec recInner (table: SymbolTable) (remaining: string list) =
        match remaining with
        | [] ->
            failwith "resolvedNameToGroupKindPrefix: empty list, shouldn't happen"
        | [ _ ] ->
            // last item, should be 'Direct' and we don't care about that - only the path prefix
            []
        | h :: etc ->
            let kind, sub =
                match table[h] with
                | Direct _ ->
                    failwithf "resolvedNameToGroupKindPrefix: encounted 'Direct' entry in middle of path?"
                | SubTable table ->
                    NormalGroup, table
                | Hybrid(thing, table) ->
                    match thing with
                    | OpaqueDef _ ->
                        OpaqueStatic, table
                    | InterfaceDef _ ->
                        InterfaceStatic, table
                    | StructDef _ ->
                        StructStatic, table
                    | _ ->
                        failwithf "resolvedNameToGroupKindPrefix:recInner: unhandled hybrid 'thing' type"
            kind :: recInner sub etc
    let table =
        match m.SymbolTable with
        | Complete table ->
            table
        | _ ->
            failwith "resolvedNameToGroupKindPrefix: symbol table not in 'Complete' state??"
    recInner table parts

let renameGroupPath (renameFunc: string -> GroupKind list -> string) (joinedPath: (string * GroupKind) list) =
    joinedPath
    |> List.mapPrefixes (fun upToPoint ->
        let namePath, kindPath =
            upToPoint
            |> List.unzip
        let name =
            namePath |> List.last
        renameFunc name kindPath)
    
type ResolveNameStringMode =
    | TypeMode
    | IdentifierMode
    | GenericIdentifierMode

type FullQualification =
    | NotForced
    | Forced of prefix: string list

let resolvedNameToStringAbstract
            (languageNsDelim: string)
            (partRenameFunc: string -> GroupKind list -> string)
            (maybePathClamp: string list option)
            (importQualification: FullQualification)
            // non-curried params:
            (context: RenderContext) (name: ResolvedName) (mode: ResolveNameStringMode) =
    
    let separator, partRenameFunc = // rebind partRenameFunc
        match mode with
        | TypeMode -> languageNsDelim, partRenameFunc
        | IdentifierMode | GenericIdentifierMode -> "_", (fun name _ -> name) // no part renaming in identifier modes
        
    // sigh, this part is a mess
    // but sadly to make things work with C++, we need the ability to rename namespace segments anywhere they might appear
    // other backend languages might not have to use this functionality
    let kindParts =
        resolvedNamePathToKindPath context.ModuleContext name
        |> List.map Some
        |> (fun x -> x @ [None]) // final segment has no group kind, should be a direct thing

    let renameParts (parts: string list) =
        let joinedPathPrefix = // not including the final named thing
            (parts, kindParts)
            ||> List.zip
            |> List.allButLast
            |> List.map (fun (name, maybeKind) -> name, Option.get maybeKind) // .allButLast prior should guarantee this always works, only final element was an Option
        let renamedPathPrefix =
            joinedPathPrefix
            |> renameGroupPath partRenameFunc
        renamedPathPrefix @ [parts |> List.last] // re-append the final element, sigh
        
    let currentContextPath =
        let path, kindPath =
            match maybePathClamp with
            | Some value ->
                // caller wants to render 'as if' at a higher/shallower level in the group tree
                value, context.GroupContext.KindPath |> List.take value.Length
            | None ->
                context.GroupContext.Path, context.GroupContext.KindPath
        // perform rename as well! otherwise it won't match / be properly stripped with List.removePrefix below :(
        List.zip path kindPath
        |> renameGroupPath partRenameFunc
        
    match name with
    | ThisModule (SymbolName parts) ->
        // if symbol is from a shallower level, check to see if it collides with anything here
        // if so, it needs to be "module-absolute"
        // check this before 'renameParts' so the symbol lookup works!
        let renderModuleAbsolute =
            if parts = context.GroupContext.Path then
                // happens if we're in eg a static block on a handle, referring to that same handle - not a problem
                false
            else
                if (parts.Length - 1) < context.GroupContext.Path.Length then
                    match findModuleSymbol context.ModuleContext.Module (context.GroupContext.Path |> List.rev) parts with
                    | Some _ ->
                        // found a collision!
                        true
                    | None ->
                        false
                else
                    false
        
        // rename the 'parts' segments now, based on their kinds
        let parts =
            renameParts parts
            
        // strip current namespace prefix if possible
        // (but not in Generics mode)
        let parts2 =
            if renderModuleAbsolute then
                match context.ModuleContext.Module.Name with
                | ModuleName moduleNameParts ->
                    moduleNameParts @ parts
            else
                match mode with
                | GenericIdentifierMode -> parts
                | _ ->
                    match List.removePrefix currentContextPath parts with
                    | Some remaining ->
                        if remaining.IsEmpty then
                            // have to leave at least one segment!
                            [ parts |> List.last ]
                        else
                            remaining
                    | None -> parts
        // do we need the approval func in this branch, too?
        joinNameParts separator (parts2 |> List.map upperFirst)
        
    | Imported(moduleName, symbolName) ->
        // rename the 'symbolParts' segments now, based on their kinds
        let symbolParts =
            match symbolName with
            | SymbolName parts ->
                // apply the segment rename func
                renameParts parts
        
        match importQualification with
        | Forced prefix ->
            let parts2 =
                match moduleName with
                | ModuleName moduleParts ->
                    (moduleParts @ symbolParts) |> List.map upperFirst
            // separator + joinNameParts separator parts2
            joinNameParts separator (prefix @ parts2)
        | NotForced ->
            let finalParts =
                if symbolIsUnambiguous context.ModuleContext symbolName then
                    symbolParts |> List.map upperFirst
                else
                    match moduleName with
                    | ModuleName moduleParts ->
                        (moduleParts @ symbolParts) |> List.map upperFirst
            joinNameParts separator finalParts

let countGroupConstants (top: GroupContext) =
    let rec groupCount (group: GroupContext) =
        let thisCount =
            group.Defs
            |> List.sumBy (function
                | Global _ -> 1
                | _ -> 0 )
        let subCount =
            group.SubOrder
            |> List.sumBy (fun name -> groupCount group.SubGroups[name])
        thisCount + subCount
    groupCount top
  
let namedBitsIndexer (info: NamedBitsInfo) (moduleContext: ModuleContext) (nameResolver: ResolvedName -> string) (rootName: string) =
    let allGroups =
        let rec inheritGroups (title: string) (info: NamedBitsInfo) =
            let parentGroups =
                match info.MaybeParent with
                | Some (Resolved (resolvedName, NamedBitsType)) ->
                    match requiredSymbolLookup moduleContext resolvedName with
                    | NamedBitsDef nextInfo ->
                        let title =
                            sprintf "%s:" (nameResolver resolvedName)
                        inheritGroups title nextInfo
                    | _ ->
                        failwith "namedbitsdef: nope"
                | _ ->
                    []
            {| Title = title; BitNames = info.BitNames |} :: parentGroups
        inheritGroups $"{rootName}:" info
        |> List.rev
    let indexedGroups, totalCount =
        let init =
            {| TotalCount = 0; RevGroups = [] |}
        (init, allGroups)
        ||> List.fold (fun acc group ->
            let startIndex =
                acc.TotalCount
            let indexedNames =
                group.BitNames
                |> List.mapi (fun i name -> (startIndex + i, name))
            let nextGroups =
                {| Title = group.Title; IndexedNames = indexedNames |} :: acc.RevGroups
            {| TotalCount = acc.TotalCount + group.BitNames.Length; RevGroups = nextGroups |})
        |> (fun acc -> acc.RevGroups |> List.rev, acc.TotalCount)
    let widthType =
        let width =
            if totalCount <= 31 then
                Width32
            else
                Width64
        Primitive(Int(true, width))
    indexedGroups, widthType

// let rec ensureAllNoDispose (context: ModuleContext) (name: ResolvedName) =
//     // since we don't have a proper validation step after parsing, we'll unfortunately do this here, when rendering
//     match requiredSymbolLookup context name with
//     | OpaqueDef info ->
//         if info.NoDispose then
//             // good, proceed up the chain
//             match info.MaybeParent with
//             | Some (Resolved(resolvedName, OpaqueType)) ->
//                 ensureAllNoDispose context resolvedName
//             | None ->
//                 // reached top, all good
//                 ()
//             | _ ->
//                 failwith "ensureAllNodispose: parent was non-opaque type"
//         else
//             failwithf "ensureAllNoDispose: opaque [%A] required to be @nodispose" name
//     | _ ->
//         failwithf "ensureAllNoDispose: symbol [%A] not opaque to begin with" name
        
let rec ensureAllNoDispose (context: ModuleContext) (name: ResolvedName) =
    // since we don't have a proper validation step after parsing, we'll unfortunately do this here, when rendering
    match requiredSymbolLookup context name with
    | OpaqueDef info ->
        if info.NoDispose then
            // good, proceed up the chain
            match info.MaybeParent with
            | Some (Resolved(resolvedName, OpaqueType)) ->
                ensureAllNoDispose context resolvedName
            | None ->
                // reached top, all good
                true
            | _ ->
                failwith "ensureAllNodispose: parent was non-opaque type"
        else
            false
    | _ ->
        failwithf "ensureAllNoDispose: symbol [%A] not opaque to begin with" name
    
let isFirstDisposableOpaque (context: ModuleContext) (info: OpaqueInfo) =
    // if this opaque is disposable (non-@nodispose),
    // is it the first one in the inheritance chain to be so?
    if info.NoDispose then
        false
    else
        // this opaque is disposable, what about the parent (if any)?
        match info.MaybeParent with
        | Some (Resolved(parentName, OpaqueType)) ->
            // verify all parents in the inheritance chain are @nodispose
            ensureAllNoDispose context parentName
            // // get the info for parent
            // match requiredSymbolLookup context parentName with
            // | OpaqueDef parentInfo ->
            //     if parentInfo.NoDispose then
            //         // kludgy: while we're at it, verify all classes above this one are @nodispose
            //         ensureAllNoDispose context parentName
            //        
            //         // our parent was @nodispose, but we are not
            //         // therefore we're the first and will need to implement IDisposable (or whatever)
            //         true
            //     else
            //         // parent was also/already disposable
            //         // so we don't need to implement anything
            //         false
            // | _ ->
            //     failwith "isFirstDisposableOpaque: parent was not an opaque type??"
        | None ->
            // no parent, so root, so yes we're the first
            true
        | _ ->
            failwith "isFirstDisposableOpaque: shouldn't happen"


type ConstExprStringConfig = {
    NullStr: string
    ScopeResStr: string
}
let typedConstExprString (config: ConstExprStringConfig) (resolvedNameToString: ResolvedName -> string) (root: TypedConstExpr) =
    let rec recInner expr =
        match expr with
        | NullValue ->
            config.NullStr
        | BoolValue value ->
            if value then "true" else "false"
        | ConstValue(namedValue, _) ->
            match namedValue with
            | ForeignName (container, name) ->
                match container with
                | Resolved(resolvedName, _) ->
                    (resolvedNameToString resolvedName) + config.ScopeResStr + name
                | _ ->
                    failwith "typedConstExprString: ForeignName wasn't resolved"
            | LocalName name ->
                name
        | Literal value ->
            match value with
            | HexNumber digits -> digits
            | DecNumber digits -> digits
        | Unary(op, value) ->
            let opStr =
                match op with
                | BitwiseNot -> "~"
                | Negation -> "-"
            sprintf "%s%s" opStr (recInner value)
        | Binary(op, left, right) ->
            let opStr =
                match op with
                | LeftShift -> "<<"
                | BitwiseAnd -> "&"
                | BitwiseXor -> "^"
                | BitwiseOr -> "|"
                | PlusOp -> "+"
            sprintf "%s %s %s" (recInner left) opStr (recInner right)
        | Grouping groupContent ->
            sprintf "(%s)" (recInner groupContent)
    recInner root

