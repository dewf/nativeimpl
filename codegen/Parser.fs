module Parser
open System
open System.IO
open ConstParser
open Tokenizer
open Extensions
open ParseCommon

type IntWidth =
    | Width8
    | Width16
    | Width32
    | Width64

let widthToInt = function
    | Width8 -> 8
    | Width16 -> 16
    | Width32 -> 32
    | Width64 -> 64
    
type NIPrimitive =
    | SizeT
    | Bool
    | Int of signed: bool * width: IntWidth
    | Float32 | Float64
    | StringType
    
type ModuleName =
    | ModuleName of string list     // x.y.whatever
with
    member this.LastPart =
        match this with
        | ModuleName parts ->
            parts |> List.rev |> List.head
    
type SymbolName =
    | SymbolName of string list
    
type ResolvedName =
    | ThisModule of name: SymbolName
    | Imported of moduleName: ModuleName * name: SymbolName
    
type UnresolvedIdentifier =
    | UnresolvedIdentifier of string list   // could be relative, could be fully qualified, etc
    
type ResolvedType =
    | OpaqueType
    | FwdOpaqueType
    | StructType
    | InterfaceType
    | ExceptionType
    | DelegateType
    | TypedefType
    | EnumType
    | NamedBitsType
    | SumType
    | OptionsType // distinct from generic 'option' type ... probably should come up with a less confusing name
    
type TypeConstraint =
    | DefaultConstraint    // everything but exceptions allowed
    | OpaqueConstraint
    | InterfaceConstraint
    | ExceptionConstraint
    | NamedBitsConstraint
    
type NIType =
    | UnitType
    | Primitive of prim: NIPrimitive
    // maybe these should be Generic of ...
    | ArrayType of elem: NIType
    | MapType of keyType: NIType * valueType: NIType
    | TupleType of tupleTypes: NIType list
    | SetType of elem: NIType
    | BufferType of elemType: NIType
    | OptionType of elem: NIType
    | ResultType of success: NIType * error: NIType
    //
    | Unresolved of identifier: UnresolvedIdentifier * loc: Location * mustBe: TypeConstraint
    | Resolved of name: ResolvedName * type_: ResolvedType

let validBufferElementType = function
    | Primitive prim ->
        prim <> StringType
    | _ ->
        false

let rec parseType (input: TokenAndLocation list) =
    match input with
    | NameStuff.NameParts (nameParts, loc, etc) -> // opaque handle, struct, interface
        Unresolved (UnresolvedIdentifier nameParts, loc, DefaultConstraint), etc
    | Lookahead 1 ([UnitToken], _, etc) ->
        UnitType, etc
    | Lookahead 1 ([SizeTToken], _, etc) ->
        Primitive SizeT, etc
    | Lookahead 1 ([BoolToken], _, etc) ->
        Primitive Bool, etc
    | Lookahead 1 ([IntToken(signed, width)], _, etc) ->
        let inner =
            let w2 =
                match width with
                | 8 -> Width8
                | 16 -> Width16
                | 32 -> Width32
                | 64 -> Width64
                | _ -> failwith "parseType intwidth??"
            Int(signed, w2)
        Primitive inner, etc
    | Lookahead 1 ([Float32Token], _, etc) ->
        Primitive Float32, etc
    | Lookahead 1 ([Float64Token], _, etc) ->
        Primitive Float64, etc
    | Lookahead 1 ([StringToken], _, etc) ->
        Primitive StringType, etc
    | Lookahead 2 ([ArrayToken; LeftAngleBracket], loc, etc1) ->
        let arrayType, etc2 =
            parseType etc1
        match etc2 with
        | Lookahead 1 ([RightAngleBracket], _, etc3) ->
            ArrayType arrayType, etc3
        | _ ->
            failwithf "failed to parse array type at %A" loc
    | Lookahead 2 ([MapToken; LeftAngleBracket], loc, etc1) ->
        let keyType, etc2 =
            parseType etc1
        match etc2 with
        | Lookahead 1 ([Comma], loc, etc3) ->
            let valueType, etc4 =
                parseType etc3
            match etc4 with
            | Lookahead 1 ([RightAngleBracket], _, etc5) ->
                MapType (keyType, valueType), etc5
            | _ ->
                failwithf "missing ] for array type at %A" loc
        | _ ->
            failwithf "missing comma for array type at %A" loc
    | Lookahead 2 ([OptionToken; LeftAngleBracket], loc, etc) ->
        let elemType, etc =
            parseType etc
        match etc with
        | Lookahead 1 ([RightAngleBracket], _, etc) ->
            OptionType elemType, etc
        | _ ->
            failwithf "failed to parse Option type at %A" loc
    | Lookahead 2 ([ResultToken; LeftAngleBracket], loc, etc) ->
        let successType, etc =
            parseType etc
        match etc with
        | Lookahead 1 ([Comma], loc, etc) ->
            let errorType, etc =
                parseType etc
            match etc with
            | Lookahead 1 ([RightAngleBracket], _, etc) ->
                ResultType (successType, errorType), etc
            | _ ->
                failwithf "missing ] for result type at %A" loc
        | _ ->
            failwithf "missing comma for result type at %A" loc
    | Lookahead 2 ([BufferToken; LeftAngleBracket], loc, etc) ->
        let elemType, etc =
            parseType etc
        if not (validBufferElementType elemType) then
            failwithf "incorrect buffer element type [%A] at %A (must be primitive type)" elemType loc
        match etc with
        | Lookahead 1 ([RightAngleBracket], _, etc) ->
            BufferType elemType, etc
        | _ ->
            failwithf "failed to parse buffer type at %A" loc
    | Lookahead 2 ([SetToken; LeftAngleBracket], loc, etc) ->
        let elemType, etc =
            parseType etc
        match etc with
        | Lookahead 1 ([RightAngleBracket], _, etc) ->
            SetType elemType, etc
        | _ ->
            failwithf "expected closing > at %A" loc
    | Lookahead 2 ([TupleToken; LeftAngleBracket], loc, etc) ->
        let content, etc =
            contentUntilMatching loc LeftAngleBracket etc
        let typeList =
            content
            |> List.unfold (fun input ->
                if input.IsEmpty then
                    None
                else
                    let t1, etc =
                        parseType input
                    match etc with
                    | [] -> // done
                        Some (t1, [])
                    | Lookahead 1 ([Comma], _, etc) -> // keep going
                        Some (t1, etc)
                    | _ ->
                        failwithf "parsing tuple: unexpected input at %A" loc)
        TupleType typeList, etc
    | [] ->
        failwithf "parseType called on an empty list!"
    | {Location = loc; Token = token} :: _ ->
        failwithf "parseType: unexpected token %A at %A" token loc
        

type Attribute =
    | NoDispose
    
type NamedFunctionArg = {
    Name: string
    Type: NIType
    DefaultValue: ConstExpr option
}

type NamedFunction = {
    Name: string
    MaybeOverloadId: int option
    Args: NamedFunctionArg list
    Returns: NIType
    Throws: NIType list
} with
    member this.WrapperName =
        match this.MaybeOverloadId with
        | Some value ->
            sprintf "%s_overload%d" this.Name value
        | None ->
            this.Name
            
type NamedField = {
    Name: string
    Type_: NIType
}

type SumTypeValue = {
    Name: string
    Args: (string * NIType) list
}

type DelegateInfo = {
    Args: (string * NIType) list
    Returns: NIType
    Throws: NIType list
}

type EnumInfo = {
    Values: (string * ConstExpr option) list // name, specified value
    MaybeWidth: NIType option
    FlagsMode: bool
}

type NamedBitsInfo = {
    MaybeParent: NIType option          // NamedBitsConstraint
    BitNames: string list
}

let isValidEnumWidth (t: NIType) =
    match t with
    | Primitive (Int _) -> true
    | _ -> false

type OpaqueInfo = {
    MaybeParent: NIType option          // opaque constraint
    Methods: NamedFunction list
    MaybeStaticDefs: ModuleDefinition list option
    NoDispose: bool
}
and InterfaceInfo = {
    MaybeParent: NIType option          // interface constraint
    Methods: NamedFunction list
    MaybeStaticDefs: ModuleDefinition list option
}
and StructInfo = {
    Fields: NamedField list
    MaybeStaticDefs: ModuleDefinition list option
}
and UserType =
    | OpaqueDef of info: OpaqueInfo
    | FwdOpaqueDef of parts: string list
    | StructDef of info: StructInfo
    | ExceptionDef of fields: NamedField list      // created as an args list, although they are accessible fields
    | InterfaceDef of info: InterfaceInfo
    | DelegateDef of info: DelegateInfo
    | TypeDef of actual: NIType
    | EnumDef of info: EnumInfo
    | NamedBitsDef of info: NamedBitsInfo
    | SumTypeDef of values: SumTypeValue list
    | OptionsDef of fields: NamedField list
and ModuleDefinition =
    | Global of name: string * type_: NIType
    | FunctionDef of NamedFunction
    | UserType of name: string * type_: UserType
    | Group of name: string * defs: ModuleDefinition list
    
type SymbolTableEntry =
    | Direct of thing: UserType
    | SubTable of table: SymbolTable
    | Hybrid of thing: UserType * table: SymbolTable
and SymbolTable =
    Map<string, SymbolTableEntry>
    
type SymbolTableState =
    | Init
    | Bootstrap of table: SymbolTable  // 1st pass, will contain unresolved names
    | Complete of table: SymbolTable   // 2nd pass, everything should be resolved

type Module = {
    Name: ModuleName
    Imports: ModuleName list
    SymbolTable: SymbolTableState
    Defs: ModuleDefinition list
}

// attribute helper functions
let applyOpaqueAttrs (attrs: Attribute list) (opaque: OpaqueInfo) =
    (opaque, attrs)
    ||> List.fold (fun info attr ->
        match attr with
        | NoDispose ->
            { info with NoDispose = true })
    

let parseOneField (input: TokenAndLocation list) =
    if input.IsEmpty then
        None
    else
        let niType, etc =
            parseType input
        match etc with
        | Lookahead 2 ([Identifier name; Semicolon], _, etc2) ->
            ({ Name = name; Type_ = niType }, etc2)
            |> Some
        | {Location = loc} :: _ ->
            failwithf "missing expected identifier at %s" (loc.ToString())
        | [] ->
            // grab location from the start of input, which had at least 1 token
            match input with
            | {Location = loc} :: _ ->
                failwithf "missing expected identifier at %s" (loc.ToString())
            | _ ->
                failwithf "parseOneField: shouldn't happen"

let parseFields (loc: Location) (input: TokenAndLocation list) =
    let content, etc =
        contentUntilMatching loc LeftBrace input
    let items =
        List.unfold parseOneField content
    items, etc
    
let (|ConstExpr|_|) (loc: Location) (input: TokenAndLocation list) =
    // grab everything up to comma or end-of-input
    let pred token =
        // not sure why we can't define this inline with the pattern match below, won't parse??
        token <> Comma
    match input with
    | TakeWhile pred (exprTokens, loc, etc) when not exprTokens.IsEmpty ->
        try
            let enumExpr =
                parseConstExpression exprTokens
            Some (enumExpr, loc, etc)
        with
            | e -> failwithf "error parsing constant expression @ %A [%s]" loc e.Message
    | _ ->
        failwithf "expected enum expression @ %A" loc

let parseArg (input: TokenAndLocation list) =
    if input.IsEmpty then
        None
    else
        let type_, etc =
            parseType input
            
        match etc with
        | Lookahead 1 ([Identifier name], loc, etc) ->
            
            // does it have a default value?
            match etc with
            | Lookahead 1 ([EqualsToken], loc, etc) ->
                match etc with
                | ConstExpr loc (expr, loc, etc) ->
                    let result =
                        { Name = name; Type = type_; DefaultValue = Some expr }
                    // consume comma, if it exists
                    match etc with
                    | Lookahead 1 ([Comma], _, etc) ->
                        Some (result, etc)
                    | [] ->
                        Some (result, [])
                    | _ ->
                        failwithf "expected comma or end of arguments @ %A" loc
                | _ ->
                    failwithf "expected constant expression while parsing function arg '%s' @ %A" name loc
                    
            | Lookahead 1 ([Comma], _, etc) ->
                Some ({ Name = name; Type = type_; DefaultValue = None }, etc)
                
            | [] ->
                // no further tokens, we're done
                Some ({ Name = name; Type = type_; DefaultValue = None }, [])
                
            | _ ->
                failwithf "parsing function argument '%s' @ %A - identifier must be followed by either comma or = <constexpr>" name loc
                
        // | [{ Location = _; Token = Identifier name }] -> // TODO: fix lookahead to use N as 'max', so it returns UP TO that many?
        //     Some ({ Name = name; Type = type_; DefaultValue = None }, [])
            
        | _ ->
            // 'rewind' to first element of input, since we know it parsed a type successfully
            match input with
            | { Location = loc } :: _ ->
                failwithf "error parsing function arg at %s: expected identifier" (loc.ToString())
            | _ ->
                failwithf "parseArg ???"

let parseArgsList (loc: Location) (input: TokenAndLocation list) =
    let argTokens, etc =
        contentUntilMatching loc LeftParen input
    let args =
        List.unfold parseArg argTokens
    args, etc

let parseNamedFunction (input: TokenAndLocation list) =
    if input.IsEmpty then
        None
    else
        let returnType, etc =
            parseType input
        match etc with
        | Lookahead 2 ([Identifier funcName; LeftParen], loc, etc) ->
            let args, etc =
                parseArgsList loc etc
            match etc with
            | Lookahead 1 ([Semicolon], _, etc) ->
                Some ({ Name = funcName; Args = args; Returns = returnType; Throws = List.empty; MaybeOverloadId = None }, etc)
            | Lookahead 1 ([ThrowsToken], loc, etc) ->
                let names, loc, etc =
                    NameStuff.parseList Comma Semicolon loc etc
                let throwsList =
                    names
                    |> List.map (fun name -> Unresolved (UnresolvedIdentifier name, loc, ExceptionConstraint))
                Some ({ Name = funcName; Args = args; Returns = returnType; Throws = throwsList; MaybeOverloadId = None }, etc)
            | _ ->
                failwithf "failed to parse function at %s" (loc.ToString())
        | { Location = loc } :: _ ->
            failwithf "was expecting identifier at %s" (loc.ToString())
        | _ ->
            // grab location from the start of input, which had at least 1 token (or it would have failed already)
            match input with
            | {Location = loc} :: _ ->
                failwithf "missing expected identifier at %s" (loc.ToString())
            | _ ->
                failwithf "parseNamedFunction: shouldn't happen"

let parseFunctionList (loc: Location) (input: TokenAndLocation list) =
    let content, etc =
        contentUntilMatching loc LeftBrace input
    let funcList =
        content
        |> List.unfold parseNamedFunction
    funcList, etc

let parseOneEnumValue (input: TokenAndLocation list) =
    match input with
    | [] ->
        // done
        None
    | Lookahead 1 ([Identifier name], _, etc) ->
        // optional user-specified numeric value?
        let value, etc =
            match etc with
            | Lookahead 1 ([EqualsToken], loc, etc) ->
                // grab everything up to comma or end-of-input
                let pred token =
                    // not sure why we can't define this inline with the pattern match below, won't parse??
                    token <> Comma
                match etc with
                | TakeWhile pred (exprTokens, loc, etc) when not exprTokens.IsEmpty ->
                    try
                        let enumExpr =
                            parseConstExpression exprTokens
                        (name, Some enumExpr), etc
                    with
                        | e -> failwithf "error parsing enum expression @ %A [%s]" loc e.Message
                | _ ->
                    failwithf "expected enum expression @ %A" loc
            | _ ->
                (name, None), etc
        // comma -> more expected?
        match etc with
        | [] ->
            // nothing left, return this final value (next will return None and terminate)
            Some (value, [])
        | Lookahead 1 ([Comma], _, etc) ->
            // more expected
            Some (value, etc)
        | { Location = loc } :: _ ->
            failwithf "parsing enum value: unexpected token at %s" (loc.ToString())
    | { Location = loc } :: _ ->
        failwithf "parsing enum value: expected identifier at %s" (loc.ToString())
    | _ ->
        failwithf "parsing enum value: ran out of tokens!"

let parseEnumValues (loc: Location) (input: TokenAndLocation list) =
    let content, etc =
        contentUntilMatching loc LeftBrace input
    let values =
        List.unfold parseOneEnumValue content
    values, etc
    
let parseNamedBitsValues (loc: Location) (input: TokenAndLocation list) =
    let content, etc =
        contentUntilMatching loc LeftBrace input
    let parseOneValue input =
        match input with
        | Lookahead 1 ([Identifier name], _, etc) ->
            // will there be more?
            match etc with
            | Lookahead 1 ([Comma], _, etc) ->
                Some (name, etc)
            | [] ->
                // no more tokens
                Some (name, [])
            | { Location = loc } :: _ ->
                // anything else
                failwithf "parsing namedbits value: missing comma before content at %s" (loc.ToString())
        | { Location = loc } :: _ ->
            // anything else
            failwithf "expecting namedbits value name at %s" (loc.ToString())
        | [] ->
            None
    let values =
        content
        |> List.unfold parseOneValue
    values, etc

let parseOneSumTypeValue (input: TokenAndLocation list) =
    match input with
    | Lookahead 1 ([Identifier name], _, etc) ->
        // does it have args?
        let args, etc2 =
            match etc with
            | Lookahead 1 ([LeftParen], loc, etc2) ->
                parseArgsList loc etc2
            | _ ->
                [], etc
                
        // 'args' is now a fancier record type (vs. the previous tuple), due to new default-value processing for named function params
        // but this isn't applicable to sumtypes, so downgrade it back to the tuple
        // in the future we probably need a separate 'parseArgList' that can work with sumtypes only
        let args2 =
            args
            |> List.map (fun arg -> (arg.Name, arg.Type))
        
        // will there be more?
        match etc2 with
        | Lookahead 1 ([Comma], _, etc3) ->
            Some ({ Name = name; Args = args2 }, etc3)
        | [] ->
            // we're done, no more tokens
            Some ({ Name = name; Args = args2 }, []) // no etc, unfold will stop
        | { Location = loc } :: _ ->
            // anything else
            failwithf "parsing sum type value: missing comma before content at %s" (loc.ToString())
    | { Location = loc } :: _ ->
        failwithf "expecting enum identifier at %s" (loc.ToString())
    | [] ->
        None

let parseSumTypeValues (loc: Location) (input: TokenAndLocation list) =
    let content, etc =
        contentUntilMatching loc LeftBrace input
    let values =
        List.unfold parseOneSumTypeValue content
    values, etc
    
type UnfoldElement<'a> =
    | Element of 'a
    | Terminator of remaining: TokenAndLocation list
    
let unfoldUntil (stopTokens: Token list) (generatorFunc: TokenAndLocation list -> ('a * TokenAndLocation list) option) (input: TokenAndLocation list) =
    let func2 (remaining: TokenAndLocation list) =
        match remaining with
        | [] ->
            None
        | Require stopTokens (_, etc) ->
            Some (Terminator etc, [])
        | { Token = tok; Location = loc } :: _ ->
            match generatorFunc remaining with
            | Some (value, remaining) ->
                Some (Element value, remaining)
            | None ->
                // I think this would only happen if 'remaining' were empty (which it won't ever be here), based on how our generators are written
                failwithf "unfoldUntil: unexpected token %A at loc %A" tok loc
    let elements, termList =
        input
        |> List.unfold func2
        |> List.partition (function | Element _ -> true | _ -> false)
    let elements =
        elements
        |> List.map (function | Element e -> e | _ -> failwith "nope")
    let etc =
        match termList |> List.head with
        | Terminator etc -> etc
        | _ -> failwith "nope"
    elements, etc
    
type OverloadMapValue = {
    Count: int
    BannedArgs: Set<NIType list>
}
let overloadMapFunc (overloads: Map<string, OverloadMapValue>) (item: NamedFunction) =
    let key =
        item.Name.ToLower()
    let argTypes =
        item.Args |> List.map (_.Type)
    if overloads.ContainsKey key then
        let value = overloads[key]
        if not (value.BannedArgs.Contains argTypes) then
            // OK to add
            let nextValue =
                { Count = value.Count + 1
                  BannedArgs = value.BannedArgs.Add argTypes }
            let nextNames =
                overloads.Add(key, nextValue)
            nextNames, { item with MaybeOverloadId = Some value.Count }
        else
            // had same args as previous function, fail
            // I know, we really need to bring the parse location into this error message,
            // but it's going to take some large-scale modification of parseModuleDefs, and I don't feel like doing that right now
            failwithf "overloadMapFunc: overload [%s] has identical argument types to a prior method/function in this scope" item.Name
    else
        let value =
            { Count = 1
              BannedArgs = Set.ofList [ argTypes ] }
        overloads.Add(key, value), item
    
let setOverloadIndices (nfList: NamedFunction list) =
    (Map.empty<string, OverloadMapValue>, nfList)
    ||> List.mapWithContext overloadMapFunc
    
let rec parseModuleDefs (input: TokenAndLocation list) =
    let rec parseNext (activeAttributes: Attribute list) (remaining: TokenAndLocation list) =
        match remaining with
        | Lookahead 2 ([AtSign; Identifier attrName], loc, etc) ->
            let attr = 
                match attrName with
                | "nodispose" -> NoDispose
                | _ -> failwithf "unrecognized attribute type '%s' @ %A" attrName loc
            parseNext (attr :: activeAttributes) etc
        | Lookahead 3 ([GroupToken; Identifier groupName; LeftBrace], loc, etc) ->
            let contentBlock, etc =
                contentUntilMatching loc LeftBrace etc
            let groupDefs =
                parseModuleDefs contentBlock
            ([Group (groupName, groupDefs)], etc)
            |> Some
        | Lookahead 1 ([FwdOpaqueToken], loc, etc) ->
            // fwdopaque Name = Module[.Group..].Symbol;
            match etc with
            | Lookahead 2 ([Identifier name; EqualsToken], loc, etc) ->
                match etc with
                | NameStuff.NameParts (fqName, loc, etc) ->
                    match etc with
                    | Lookahead 1 ([Semicolon], _, etc) ->
                        ([UserType (name, FwdOpaqueDef fqName)], etc)
                        |> Some
                    | _ ->
                        failwithf "parsing fwdopaque @ %A - expected semicolon" loc
                | _ ->
                    failwithf "parsing fwdopaque @ %A - expected fully qualified name (for external symbol)" loc
            | Lookahead 1 ([Identifier _], loc, _) ->
                failwithf "parsing fwdopaque @ %A - expected = <external symbol>;" loc
            | _ ->
                failwithf "parsing fwdopaque @ %A - expected: <identifier> = ..." loc
        | Lookahead 2 ([OpaqueToken; Identifier opaqueName], loc, etc) ->
            // inherits anything?
            let maybeParent, loc, etc =
                match etc with
                | Lookahead 1 ([ExtendsToken], loc, etc) ->
                    match etc with
                    | NameStuff.NameParts (parts, loc, etc) ->
                        let type_ =
                            Unresolved (UnresolvedIdentifier parts, loc, OpaqueConstraint)
                        Some type_, loc, etc
                    | _ ->
                        failwithf "expected parent opaque name @ %A" loc
                | _ ->
                    None, loc, etc
            // continue as normal
            match etc with
            // empty opaque
            | Lookahead 1 ([Semicolon], _, etc) ->
                let info =
                    { MaybeParent = maybeParent; Methods = []; MaybeStaticDefs = None; NoDispose = false }
                    |> applyOpaqueAttrs activeAttributes
                ([UserType (opaqueName, OpaqueDef info)], etc)
                |> Some
            // opaque with content (methods + optional static {} block)
            | Lookahead 1 ([LeftBrace], loc, etc) ->
                // first, figure out the limits of the opaque content block as a whole
                let contentBlock, afterOpaqueBlock =
                    contentUntilMatching loc LeftBrace etc

                // opaques optionally begin with static block
                let maybeStaticDefs, methodBlock =
                    match contentBlock with
                    | Lookahead 2 ([StaticToken; LeftBrace], loc, etc) ->
                        let staticContent, afterStatic =
                            contentUntilMatching loc LeftBrace etc
                        let maybeDefs =
                            let parsed =
                                parseModuleDefs staticContent
                            if parsed.IsEmpty then
                                None
                            else
                                Some parsed
                        maybeDefs, afterStatic
                    | _ ->
                        None, contentBlock
                        
                // methods should always be present
                let methods =
                    methodBlock
                    |> List.unfold parseNamedFunction
                    |> setOverloadIndices
                    
                let info =
                    { MaybeParent = maybeParent
                      Methods = methods
                      MaybeStaticDefs = maybeStaticDefs
                      NoDispose = false }
                    |> applyOpaqueAttrs activeAttributes
                ([UserType (opaqueName, OpaqueDef info)], afterOpaqueBlock)
                |> Some
            | _ ->
                failwithf "error parsing opaque token at %s" (loc.ToString())
        | Lookahead 3 ([StructToken; Identifier name; LeftBrace], loc, etc) ->
            // optional static block
            let maybeStaticDefs, etc =
                match etc with
                | Lookahead 2 ([StaticToken; LeftBrace], _, etc) ->
                    let defsList, etc  =
                        unfoldUntil [RightBrace] (parseNext []) etc
                    let flattened =
                        defsList
                        |> List.collect id
                    let maybeStaticDefs =
                        if not flattened.IsEmpty then Some flattened else None
                    maybeStaticDefs, etc
                | _ ->
                    None, etc
            let fields, etc2 =
                parseFields loc etc
            if fields.Length > 0 then
                let info =
                    { Fields = fields; MaybeStaticDefs = maybeStaticDefs }
                ([UserType (name, StructDef info)], etc2)
                |> Some
            else
                failwithf "struct '%s' @ loc %A - no fields declared" name loc
        | Lookahead 2 ([ExceptionToken; Identifier name], loc, etc) ->
            // does it have arguments? (these become fields)
            let args, etc =
                match etc with
                | Lookahead 1 ([Semicolon], _, etc) -> // nope
                    [], etc
                | Lookahead 1 ([LeftParen], loc, etc) ->
                    let args, etc =
                        parseArgsList loc etc
                    match etc with
                    | Lookahead 1 ([Semicolon], _, etc) ->
                        args, etc
                    | _ ->
                        failwithf "expected statement end at %s" (loc.ToString())
                | _ ->
                    failwithf "error parsing exception: expecting statement end or arguments list, at %s" (loc.ToString())
            let fields =
                args |> List.map (fun arg -> { Name = arg.Name; Type_ = arg.Type })
            ([UserType (name, ExceptionDef fields)], etc)
            |> Some
        | Lookahead 2 ([InterfaceToken; Identifier name], loc, etc) ->
            // inherits anything?
            let maybeParent, loc, etc =
                match etc with
                | Lookahead 1 ([ExtendsToken], loc, etc) ->
                    match etc with
                    | NameStuff.NameParts (parts, loc, etc) ->
                        let type_ =
                            Unresolved (UnresolvedIdentifier parts, loc, InterfaceConstraint)
                        Some type_, loc, etc
                    | _ ->
                        failwithf "expected parent interface name @ %A" loc
                | _ ->
                    None, loc, etc
            // continue as normal
            match etc with
            | Lookahead 1 ([Semicolon], loc, etc) ->
                // we'll allow empty interfaces if they inherit from something
                // (just because I haven't given much thought to truly empty interfaces, or what they'd be useful for)
                match maybeParent with
                | Some _ ->
                    let info = {
                        MaybeParent = maybeParent
                        Methods = []
                        MaybeStaticDefs = None
                    }
                    ([UserType (name, InterfaceDef info)], etc)
                    |> Some
                | None ->
                    failwithf "totally empty interface @ %A - right now these aren't allowed, must contain either a single method or extend a parent interface" loc
            | Lookahead 1 ([LeftBrace], loc, etc) ->
                // optional static block
                let maybeStaticDefs, etc =
                    match etc with
                    | Lookahead 2 ([StaticToken; LeftBrace], _, etc) ->
                        let defsList, etc  =
                            unfoldUntil [RightBrace] (parseNext []) etc
                        let flattened =
                            defsList
                            |> List.collect id
                        let maybeStaticDefs =
                            if not flattened.IsEmpty then Some flattened else None
                        maybeStaticDefs, etc
                    | _ ->
                        None, etc
                let methods, etc =
                    parseFunctionList loc etc
                let methods =
                    methods
                    |> setOverloadIndices
                if methods.Length > 0 || maybeParent.IsSome then // empty method block allowed when inheriting from another
                    let info =
                        { MaybeParent = maybeParent
                          Methods = methods
                          MaybeStaticDefs = maybeStaticDefs }
                    ([UserType (name, InterfaceDef info)], etc)
                    |> Some
                else
                    failwithf "interface '%s' @ loc %A - no methods declared (and none inherited)" name loc
            | _ ->
                failwithf "expected '{' at %A" loc
        | Lookahead 1 ([DelegateToken], _, etc) ->
            parseNamedFunction etc
            |> Option.map (fun (nf, etc2) ->
                let result =
                    let tupledArgs = // downgrade from new default-value-capable function args
                        nf.Args
                        |> List.map (fun arg -> (arg.Name, arg.Type))
                    UserType (nf.Name, DelegateDef { Args = tupledArgs; Returns = nf.Returns; Throws = nf.Throws })
                ([result], etc2))
        | Lookahead 1 ([TypedefToken], loc, etc) ->
            let actual, etc2 =
                parseType etc
            match etc2 with
            | Lookahead 2 ([Identifier name; Semicolon], _, etc3) ->
                ([UserType (name, TypeDef actual)], etc3)
                |> Some
            | _ ->
                failwithf "expected typedef name at %s" (loc.ToString())
        | Lookahead 2 ([EnumToken | FlagsToken as which; Identifier name], loc, etc) ->
            let maybeWidthType, etc =
                match etc with
                | Lookahead 1 ([Colon], loc, etc) ->
                    let widthType, etc =
                        parseType etc
                    if not (isValidEnumWidth widthType) then
                        failwithf "enum/flags: invalid width type (must be integral type) at %s" (loc.ToString())
                    match etc with
                    | Lookahead 1 ([LeftBrace], _, etc) ->
                        Some widthType, etc
                    | _ ->
                        failwithf "enum/flags: expecting left brace { at %s" (loc.ToString())
                | Lookahead 1 ([LeftBrace], _, etc) ->
                    None, etc
                | _ ->
                    failwithf "enum/flags: expecting int width or opening { at %s" (loc.ToString())
            let values, etc =
                parseEnumValues loc etc
            ([UserType (name, EnumDef {Values = values; MaybeWidth = maybeWidthType; FlagsMode = (which = FlagsToken)})], etc)
            |> Some
        | Lookahead 2 ([NamedBitsToken; Identifier name], loc, etc) ->
            // inherits anything?
            let maybeParent, loc, etc =
                match etc with
                | Lookahead 1 ([ExtendsToken], loc, etc) ->
                    match etc with
                    | NameStuff.NameParts (parts, loc, etc) ->
                        let type_ =
                            Unresolved (UnresolvedIdentifier parts, loc, NamedBitsConstraint)
                        Some type_, loc, etc
                    | _ ->
                        failwithf "expected parent namedbits name @ %A" loc
                | _ ->
                    None, loc, etc
            // continue as normal
            match etc with
            | Lookahead 1 ([Semicolon], _, etc) ->
                // no content, must inherit (for now)
                match maybeParent with
                | Some parent ->
                    let info =
                        { MaybeParent = Some parent; BitNames = [] }
                    ([UserType (name, NamedBitsDef info)], etc)
                    |> Some
                | None ->
                    failwithf "namedbits: expected 'extends [parentname]' or { name1, name2, ... }"
            | Lookahead 1 ([LeftBrace], loc, etc) ->
                let bitNames, etc =
                    parseNamedBitsValues loc etc
                let info =
                    { MaybeParent = maybeParent; BitNames = bitNames }
                ([UserType (name, NamedBitsDef info)], etc)
                |> Some
            | _ ->
                failwithf "namedbits: expected left brace { at %s" (loc.ToString())
        | Lookahead 3 ([SumTypeToken; Identifier name; LeftBrace], loc, etc) ->
            let values, etc2 =
                parseSumTypeValues loc etc
            ([UserType (name, SumTypeDef values)], etc2)
            |> Some
        | Lookahead 3 ([OptionsToken; Identifier name; LeftBrace], loc, etc) ->
            let fields, etc2 =
                parseFields loc etc
            ([UserType (name, OptionsDef fields)], etc2)
            |> Some
        | [] ->
            // end of file!
            None
        | etc ->
            // either a global or a function, always starts with a type
            let t1, etc2 =
                parseType etc
            match etc2 with
            | Lookahead 2 ([Identifier name; Semicolon], _, etc3) ->
                ([Global (name, t1)], etc3)
                |> Some
            | Lookahead 2 ([Identifier _; LeftParen], _, _) ->
                parseNamedFunction etc // note using 'etc' here (rewinding a bit because parseNamedFunction handles identifier etc too)
                |> Option.map (fun (namedFunc, etc3) -> [FunctionDef namedFunc], etc3)
                // only way it returns 'None' is if its token list is empty, which we already know isn't the case
            | {Location = loc; Token = token} :: _ ->
                failwithf "unexpected token %A at %s" token (loc.ToString())
            | [] ->
                failwith "unexpected end of file"
    let rawDefs =
        List.unfold (parseNext []) input
        |> List.collect id
    // process any module-level/static block overloads (opaque and interface methods already handled above)
    (Map.empty, rawDefs)
    ||> List.mapWithContext (fun names md ->
        match md with
        | FunctionDef nf ->
            let nextNames, nextFunc =
                overloadMapFunc names nf
            nextNames, FunctionDef nextFunc
        | _ ->
            names, md)

let consumeImports (input: TokenAndLocation list) =
    let rec nextImport (revImports: ModuleName list) (remaining: TokenAndLocation list) =
        match remaining with
        | Lookahead 1 ([ImportToken], loc, etc) ->
            match etc with
            | NameStuff.NameParts (nameParts, loc, etc) ->
                match etc with
                | Lookahead 1 ([Semicolon], _, etc) ->
                    // OK!
                    nextImport (ModuleName nameParts :: revImports) etc
                | _ ->
                    failwithf "expected semicolon after module identifier @ %A" loc
            | _ ->
                failwithf "expected module name identifier @ %A" loc
        | _ ->
            revImports, remaining
    let rev, etc =
        nextImport [] input
    List.rev rev, etc
    
let parseModuleToplevel (input: TokenAndLocation list) (outerName: ModuleName) =
    match input with
    | Lookahead 1 ([ModuleToken], loc, etc) ->
        match etc with
        | NameStuff.NameParts (nameParts, loc, etc) ->
            match etc with
            | Lookahead 1 ([Semicolon], _, etc) ->
                // OK!
                let internalName =
                    ModuleName nameParts
                if internalName <> outerName then
                    failwithf "internal module name %A did not match outer name/location %A" nameParts outerName
                // imports, if any must occur at top of file after "module"
                let imports, etc =
                    consumeImports etc
                { Name = internalName
                  Imports = imports
                  SymbolTable = Init
                  Defs = parseModuleDefs etc }
            | _ ->
                failwithf "expected semicolon after module definition @ %s" (loc.ToString())
        | _ ->
            failwithf "expected module name identifier @ %s" (loc.ToString())
    | _ ->
        failwith "parseModuleToplevel: expected module definition at beginning of file"

// resolution stuff ===========================
type ModuleContext = {
    Loaded: Map<ModuleName, Module>
    Module: Module
}

let findSymbolHere (table: SymbolTable) (nameParts: string list) =
    let rec recFind (currentTable: SymbolTable) (partsRemain: string list) =
        match partsRemain with
        | name :: etc ->
            match currentTable.TryFind name with
            | Some entry ->
                match etc, entry with
                | [], (Direct thing | Hybrid (thing, _)) ->
                    // direct hit, OK
                    Some thing
                | [], SubTable _ ->
                    // ran out of path parts, but at a group/subtable, not a specific symbol
                    None
                | _ :: _, Direct _ ->
                    // had more name parts, but dead-ended (prevented from going further)
                    None
                | more, (SubTable subTable | Hybrid (_, subTable)) ->
                    // recurse to consume name parts
                    recFind subTable more
            | None ->
                // current table label contained no such name, can't continue
                None
        | [] ->
            failwith "findHere::recFind - empty partsLeft, shouldn't happen"
    recFind table nameParts
    
let findModuleSymbol (m: Module) (revNamespacePath: string list) (nameParts: string list) =
    let table =
        match m.SymbolTable with
        | Bootstrap table -> table
        | Complete table -> table
        | Init ->
            failwith "findModuleSymbol: attempted to use a symbol table in Init state??"
    // we iteratively search for [nameParts] from deeper to shallower (removing one element of revNamespacePath each time)
    let nsPath =
        revNamespacePath |> List.rev
    let rec findDepthFirst (current: SymbolTable) (revConsumedPath: string list) (nsPathLeft: string list) =
        // go deep first, exhausting the nsPath
        match nsPathLeft with
        | [] ->
            // end of nsPath, see what we can find relative to current table
            match findSymbolHere current nameParts with
            | Some value ->
                // return w/ consumed path prefix (needed for resolved names, want fully qualified path)
                Some (List.rev revConsumedPath, value)
            | None ->
                None
        | name :: etc ->
            match current.TryFind name with
            | Some entry ->
                match etc, entry with
                | _, (SubTable subTable | Hybrid (_, subTable)) ->
                    match findDepthFirst subTable (name :: revConsumedPath) etc with
                    | Some value ->
                        // found something deeper, great!
                        Some value
                    | None ->
                        // didn't find anything at the deeper level - what about here?
                        match findSymbolHere current nameParts with
                        | Some value ->
                            // return with consumed path prefix
                            Some (List.rev revConsumedPath, value)
                        | None ->
                            // back out and keep searching
                            None
                | _, Direct _ ->
                    // namespace path terminated in a direct symbol hit - shouldn't happen (namespace path always refers to enclosing table)
                    failwithf "tryFindSymbol: namespace element '%s' terminated in direct symbol entry - shouldn't happen" name
            | None ->
                failwithf "tryFindSymbol: namespace element '%s' did not exist at current level of symbol table - shouldn't happen" name
    findDepthFirst table [] nsPath
    |> Option.map (fun (consumed, thing) ->
        // return name relative to module root
        consumed @ nameParts, thing)
    
type AmbiguityMode =
    | DirectImportsOnly
    | RecursiveImports
    
type AmbiguityMemoKey = {
    ModuleName: ModuleName
    SymbolName: string list // SymbolName but we're already deconstructing it as a function parameter below
    Mode: AmbiguityMode
}

let mutable ambiguityMemoMap: Map<AmbiguityMemoKey, bool> = Map.empty

let symbolIsUnambiguous (context: ModuleContext) (SymbolName nameParts) (mode: AmbiguityMode) =
    // memoized for speed, this is going to be called many times
    let innerFunc () =
        let imported =
            match mode with
            | DirectImportsOnly ->
                context.Module.Imports
                |> List.map (fun moduleName ->
                    match context.Loaded.TryFind moduleName with
                    | Some value ->
                        value
                    | None ->
                        failwithf "symbolIsUnambiguous (direct import mode): couldn't find modulename %A in the 'Loaded' module map ??" moduleName)
            | RecursiveImports ->
                let rec recImportFunc m =
                    m.Imports
                    |> List.collect (fun moduleName ->
                        match context.Loaded.TryFind moduleName with
                        | Some value ->
                            value :: recImportFunc value
                        | None ->
                            failwithf "symbolIsUnambiguous (recursive import mode): couldn't find modulename %A in the 'Loaded' module map ??" moduleName)
                recImportFunc context.Module
                |> List.distinct // important!
        let toSearch =
            context.Module :: imported
        let found =
            toSearch
            |> List.choose (fun m -> findModuleSymbol m [] nameParts)
        found.Length < 2
    // check for existing ...
    let key = {
        ModuleName = context.Module.Name
        SymbolName = nameParts
        Mode = mode
    }
    match ambiguityMemoMap.TryFind key with
    | Some result ->
        result
    | None ->
        let result =
            innerFunc()
        ambiguityMemoMap <- ambiguityMemoMap.Add(key, result)
        result

type ImportResult = {
    ImportName: ModuleName
    Result: string list * UserType
}

let removeModuleName (ModuleName moduleNameParts) (nameParts: string list) =
    match List.skipWhile2 (fun a b -> a = b) moduleNameParts nameParts with
    | _, remaining ->
        remaining

let resolveIdentifier (context: ModuleContext) (loc: Location) (namespacePath: string list) (UnresolvedIdentifier nameParts) =
    // 1. try to find in the current module
    match findModuleSymbol context.Module namespacePath nameParts with
    | Some (fullName, thing) ->
        let symbolName =
            SymbolName fullName
        (ThisModule symbolName, thing)
    | None ->
        // 2. not in this module, what about imports?
        let importResults =
            context.Module.Imports
            |> List.choose (fun importName ->
                let import =
                    context.Loaded[importName]
                // first look for it as-is, as a relative indentifier within the module (vs. fully qualified)
                // because what if we're looking for something that starts with the same name as the module itself? don't want to strip that prematurely
                match findModuleSymbol import [] nameParts with
                | Some res ->
                    Some { ImportName = importName; Result = res }
                | None ->
                    // no luck that way, now try stripping any common prefix between identifier and full module path, in case the user is attempting to use a fully qualified name
                    // (BTW, is this the correct precedence? or should it be reversed, and we should look for a fully qualified thing first, and only if that fails resort to a relative name?
                    match removeModuleName importName nameParts with
                    | [] ->
                        // module name completely consumed nameParts, there would be nothing left to search for!
                        // seems like an odd thing to happen, how could an identifier refer to something that starts with the name of a module, but doesn't refer to the module? seems like an error ...
                        // well, what if module A exports a symbol that has the same name as module B?
                        None
                    | relativeParts ->
                        findModuleSymbol import [] relativeParts
                        |> Option.map (fun res ->
                            { ImportName = importName; Result = res }))
        match importResults with
        | _ :: _ :: _ ->
            // 2+ results, ambiguous = not OK
            let matchedNames =
                importResults
                |> List.map (fun { ImportName = name } -> name)
            failwithf "resolveIdentifier: identifier %A in module %A matched symbols in multiple imports %A @ %A" nameParts context.Module.Name matchedNames loc
        | [ { ImportName = importName; Result = (fullName, thing) } ] ->
            // single result, OK
            (Imported (importName, SymbolName fullName), thing)
        | [] ->
            // not found at all!
            failwithf "resolveIdentifier: identifier %A not found in containing module %A nor in any imports @ %A" nameParts context.Module.Name loc

let rec resolveType (context: ModuleContext) (namespacePath: string list) (type_: NIType) =
    let applyNested =
        resolveType context namespacePath
    match type_ with
    | UnitType ->
        type_
    | Primitive _ ->
        type_
    | BufferType _ ->
        type_
    | Resolved _ ->
        type_
    // apply to things which contain nested NITypes
    | ArrayType niType ->
        ArrayType (applyNested niType)
    | MapType (keyType, valueType) ->
        MapType (applyNested keyType, applyNested valueType)
    | TupleType tupleTypes ->
        TupleType (tupleTypes |> List.map applyNested)
    | SetType niType ->
        SetType (applyNested niType)
    | OptionType elem ->
        OptionType (applyNested elem)
    | ResultType(success, error) ->
        ResultType (applyNested success, applyNested error)
    | Unresolved (nameParts, loc, typeConstraint) ->
        match resolveIdentifier context loc namespacePath nameParts with
        | resolvedName, resolvedDef ->
            match typeConstraint with
            | DefaultConstraint ->
                match resolvedDef with
                | ExceptionDef _ ->
                    failwithf "type constraint failed @ %A - exception type not allowed here" loc
                | _ ->
                    () // OK
            | OpaqueConstraint ->
                match resolvedDef with
                | OpaqueDef _ ->
                    () // OK
                | _ ->
                    failwithf "type constraint failed @ %A - must be 'opaque' type" loc
            | InterfaceConstraint ->
                match resolvedDef with
                | InterfaceDef _ ->
                    () // OK
                | _ ->
                    failwithf "type constraint failed @ %A - must be 'interface' type" loc
            | NamedBitsConstraint ->
                match resolvedDef with
                | NamedBitsDef _ ->
                    () // OK
                | _ ->
                    failwithf "type constraint failed @ %A - must be 'namedbits' type" loc
            | ExceptionConstraint ->
                match resolvedDef with
                | ExceptionDef _ ->
                    () // OK
                | _ ->
                    failwithf "type constraint failed @ %A - must be 'exception' type" loc
            let resolvedType =
                match resolvedDef with
                | FwdOpaqueDef _ -> FwdOpaqueType
                | OpaqueDef _ -> OpaqueType
                | StructDef _ -> StructType
                | InterfaceDef _ -> InterfaceType
                | DelegateDef _ -> DelegateType
                | TypeDef _ -> TypedefType
                | EnumDef _ -> EnumType
                | NamedBitsDef _ -> NamedBitsType
                | SumTypeDef _ -> SumType
                | OptionsDef _ -> OptionsType
                | ExceptionDef _ -> ExceptionType
            Resolved (resolvedName, resolvedType)

let resolveFieldType (context: ModuleContext) (namespacePath: string list) (ft: NamedField) =
    let nextType =
        resolveType context namespacePath ft.Type_
    { ft with Type_ = nextType }

let resolveFunctionTypes (context: ModuleContext) (namespacePath: string list) (nf: NamedFunction) =
    let nextArgs =
        nf.Args
        |> List.map (fun arg -> { arg with Type = resolveType context namespacePath arg.Type })
    let nextReturns =
        resolveType context namespacePath nf.Returns
    let nextThrows =
        nf.Throws
        |> List.map (resolveType context namespacePath)
    { nf with Args = nextArgs; Returns = nextReturns; Throws = nextThrows }

let resolveDelegateTypes (context: ModuleContext) (namespacePath: string list) (info: DelegateInfo) =
    let nextArgs =
        info.Args
        |> List.map (fun (name, argType) -> (name, resolveType context namespacePath argType))
    let nextReturns =
        resolveType context namespacePath info.Returns
    let nextThrows =
        info.Throws
        |> List.map (resolveType context namespacePath)
    { Args = nextArgs; Returns = nextReturns; Throws = nextThrows }

let resolveSumTypeTypes (context: ModuleContext) (namespacePath: string list) (values: SumTypeValue list) =
    values
    |> List.map (fun value ->
        let nextArgs =
            value.Args
            |> List.map (fun (name, type_) ->
                name, resolveType context namespacePath type_)
        { value with Args = nextArgs })

let rec resolveModuleDef (context: ModuleContext) (namespacePath: string list) (def: ModuleDefinition) =
    match def with
    | Group (name, defs) ->
        let subPath =
            name :: namespacePath
        let nextDefs =
            defs
            |> List.map (resolveModuleDef context subPath)
        Group (name, nextDefs)
    | UserType (name, userType) ->
        let nextUserType =
            match userType with
            | StructDef info ->
                let nextFields, nextStaticDefs =
                    match info.MaybeStaticDefs with
                    | Some staticDefs ->
                        // if static block exists, method defs need to be evaluated in its context
                        let subPath =
                            name :: namespacePath
                        let nextFields =
                            info.Fields
                            |> List.map (resolveFieldType context subPath)
                        let nextStaticDefs =
                            staticDefs
                            |> List.map (resolveModuleDef context subPath)
                        nextFields, Some nextStaticDefs
                    | None ->
                        let nextFields =
                            info.Fields
                            |> List.map (resolveFieldType context namespacePath)
                        nextFields, None
                let nextInfo =
                    { info with Fields = nextFields; MaybeStaticDefs = nextStaticDefs }
                StructDef nextInfo
            | OptionsDef fieldTypes ->
                let nextFieldTypes =
                    fieldTypes
                    |> List.map (resolveFieldType context namespacePath)
                OptionsDef nextFieldTypes
            | ExceptionDef fieldTypes ->
                let nextFieldTypes =
                    fieldTypes
                    |> List.map (resolveFieldType context namespacePath)
                ExceptionDef nextFieldTypes
            | InterfaceDef info ->
                let nextMaybeParent =
                    info.MaybeParent
                    |> Option.map (resolveType context namespacePath)
                let nextMethods, nextStaticDefs =
                    match info.MaybeStaticDefs with
                    | Some staticDefs ->
                        // if static block exists, method defs need to be evaluated in its context
                        // otherwise method params (eg associated enums) won't resolve!
                        let subPath =
                            name :: namespacePath
                        let nextMethods =
                            info.Methods
                            |> List.map (resolveFunctionTypes context subPath)
                        let nextStaticDefs =
                            staticDefs
                            |> List.map (resolveModuleDef context subPath)
                        nextMethods, Some nextStaticDefs
                    | None ->
                        let nextMethods =
                            info.Methods
                            |> List.map (resolveFunctionTypes context namespacePath)
                        nextMethods, None
                let nextInfo =
                    { MaybeParent = nextMaybeParent; Methods = nextMethods; MaybeStaticDefs = nextStaticDefs }
                InterfaceDef nextInfo
            | DelegateDef delegateInfo ->
                let nextInfo =
                    resolveDelegateTypes context namespacePath delegateInfo
                DelegateDef nextInfo
            | FwdOpaqueDef parts ->
                // no types to resolve
                FwdOpaqueDef parts
            | OpaqueDef info ->
                let nextMaybeParent =
                    info.MaybeParent
                    |> Option.map (resolveType context namespacePath)
                let nextMethods, nextStaticDefs =
                    match info.MaybeStaticDefs with
                    | Some staticDefs ->
                        // if static block exists, method defs need to be evaluated in its context
                        // otherwise method params (eg associated enums) won't resolve!
                        let subPath =
                            name :: namespacePath
                        let nextMethods =
                            info.Methods
                            |> List.map (resolveFunctionTypes context subPath)
                        let nextStaticDefs =
                            staticDefs
                            |> List.map (resolveModuleDef context subPath)
                        nextMethods, Some nextStaticDefs
                    | None ->
                        let nextMethods =
                            info.Methods
                            |> List.map (resolveFunctionTypes context namespacePath)
                        nextMethods, None
                let nextInfo =
                    { info with MaybeParent = nextMaybeParent; Methods = nextMethods; MaybeStaticDefs = nextStaticDefs }
                OpaqueDef nextInfo
            | TypeDef niType ->
                TypeDef (resolveType context namespacePath niType)
            | EnumDef values ->
                EnumDef values
            | NamedBitsDef info ->
                let nextMaybeParent =
                    info.MaybeParent
                    |> Option.map (resolveType context namespacePath)
                let nextInfo =
                    { info with MaybeParent = nextMaybeParent }
                NamedBitsDef nextInfo
            | SumTypeDef enumValues ->
                let nextValues =
                    resolveSumTypeTypes context namespacePath enumValues
                SumTypeDef nextValues
        UserType (name, nextUserType)
    | Global (name, niType) ->
        Global (name, resolveType context namespacePath niType)
    | FunctionDef namedFunction ->
        FunctionDef (resolveFunctionTypes context namespacePath namedFunction)

let rec getUserTypes (defs: ModuleDefinition list) =
    defs
    |> List.choose (function
        | Group (name, defs) ->
            let subTable =
                getUserTypes defs
            Some (name, SubTable subTable)
        | UserType (name, definition) ->
            match definition with
            | OpaqueDef info ->
                match info.MaybeStaticDefs with
                | Some staticDefs ->
                    let subTable =
                        getUserTypes staticDefs
                    Some (name, Hybrid (definition, subTable))
                | None ->
                    Some (name, Direct definition)
            | InterfaceDef info ->
                match info.MaybeStaticDefs with
                | Some staticDefs ->
                    let subTable =
                        getUserTypes staticDefs
                    Some (name, Hybrid (definition, subTable))
                | None ->
                    Some (name, Direct definition)
            | StructDef info ->
                match info.MaybeStaticDefs with
                | Some staticDefs ->
                    let subTable =
                        getUserTypes staticDefs
                    Some (name, Hybrid (definition, subTable))
                | None ->
                    Some (name, Direct definition)
            | _ ->
                Some (name, Direct definition)
        | _ -> None)
    |> Map.ofList

let resolveUserTypes (loaded: Map<ModuleName, Module>) (module_: Module) =
    let nextModule =
        let symbols =
            getUserTypes module_.Defs
        { module_ with SymbolTable = Bootstrap symbols }
    let context =
        { Loaded = loaded; Module = nextModule }
    // replace all Unresolved types with things from the table, if they even exist
    let nextDefs =
        nextModule.Defs
        |> List.map (resolveModuleDef context [])
    // the actual module definitions have been resolved, but the symbol table still contains only the original unresolved content
    // ie, the bootstrap definitions (names -> original unresolved module defs)
    // so this is a 2nd pass that will bring that up to date
    let nextUserTypes =
        getUserTypes nextDefs
    { nextModule with Defs = nextDefs; SymbolTable = Complete nextUserTypes }
    
let moduleNameToFilename (rootPath: string) (ModuleName nameParts) =
    let pathParts =
        String.Join("/", nameParts)
    $"{rootPath}/{pathParts}.nimpl"

let rec loadModule (rootPath: string) (moduleMap: Map<ModuleName, Module>) (name: ModuleName) (forbidden: Set<ModuleName>) =
    match moduleMap.TryFind name with
    | Some m ->
        // already loaded!
        m, moduleMap
    | None ->
        printfn "loading %A" name
        let filename =
            moduleNameToFilename rootPath name
        let tokens =
            File.ReadAllText filename
            |> tokenize
        let parsed =
            parseModuleToplevel tokens name
        // load imports before performing resolution
        let nextMap =
            let nextForbidden =
                // add this module to the forbidden list, which will be passed to imports to prevent cycles
                forbidden.Add name
            (moduleMap, parsed.Imports)
            ||> List.fold (fun acc importName ->
                // verify not in forbidden (to prevent circular dependencies)
                if forbidden.Contains importName then
                    failwithf "module %A cannot import %A - would create a dependency cycle" name importName
                else
                    loadModule rootPath acc importName nextForbidden
                    |> snd)
        // perform resolution against our imports
        let resolved =
            parsed
            |> resolveUserTypes nextMap
        // return updated module map
        resolved, nextMap.Add(name, resolved)
