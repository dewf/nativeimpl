module Parser
open System
open System.IO
open Tokenizer
open Extensions

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
    
type SymbolName =
    | SymbolName of string list
    
type ResolvedName =
    | ThisModule of name: SymbolName
    | Imported of moduleName: ModuleName * name: SymbolName
    
type UnresolvedIdentifier =
    | UnresolvedIdentifier of string list   // could be relative, could be fully qualified, etc
    
type ResolvedType =
    | OpaqueType
    | StructType
    | InterfaceType
    | ExceptionType
    | DelegateType
    | TypedefType
    | EnumType
    | SumType
    | OptionsType
    
type TypeConstraint =
    | DefaultConstraint    // everything but exceptions allowed
    | OpaqueConstraint
    | ExceptionConstraint
    
type NIType =
    | UnitType
    | Primitive of prim: NIPrimitive
    // maybe these should be Generic of ...
    | ArrayType of elem: NIType
    | MapType of keyType: NIType * valueType: NIType
    | TupleType of tupleTypes: NIType list
    | SetType of elem: NIType
    | BufferType of elemType: NIType
    //
    | Unresolved of identifier: UnresolvedIdentifier * loc: Location * mustBe: TypeConstraint
    | Resolved of name: ResolvedName * type_: ResolvedType

let validBufferElementType = function
    | Primitive prim ->
        prim <> StringType
    | _ ->
        false

let (|Lookahead|_|) (n: int) (input: TokenAndLocation list) = // returns ([tokens], loc, etc)
    let front, etc =
        List.safeSplit n input
    if front.Length = n then
        let tokens =
            front |> List.map (_.Token)
        Some (tokens, front.Head.Location, etc)
    else
        None
        
let (|TakeWhile|_|) (pred: Token -> bool) (input: TokenAndLocation list) = // returns ([tokens], loc, etc)
    let front, etc =
        input
        |> List.splitWhile (fun tl -> pred tl.Token)
    match front with
    | h :: _ ->
        // has at least 1 item
        let tokens =
            front |> List.map (_.Token)
        Some (tokens, h.Location, etc)
    | _ ->
        None
        
// let (|TakeWhileAndVerify|_|) (pred: Token -> bool) (verify: Token list -> bool) (input: TokenAndLocation list) = // returns ([tokens], loc, etc)
//     match input with
//     | TakeWhile pred (tokens, loc, etc) when verify(tokens) ->
//         Some (tokens, loc, etc)
//     | _ ->
//         None
        
let splitTokensBefore (matcher: TokenAndLocation list -> bool) (input: TokenAndLocation list) =
    let rec tryMatch (revBefore: TokenAndLocation list) (remaining: TokenAndLocation list) =
        if matcher remaining then
            (List.rev revBefore, remaining)
            |> Some
        else
            match remaining with
            | [] ->
                None
            | h :: etc ->
                tryMatch (h :: revBefore) etc
    tryMatch [] input

let whileDoesParse (tryParseOne: TokenAndLocation list -> Option<'a * Location * TokenAndLocation list * bool>) (input: TokenAndLocation list) = // : 'a list * TokenAndLocation list =
    // apply the function 'f' to input until it fails, returning its generated elements and the remaining 'etc' list
    let rawParsed =
        input
        |> List.unfold (fun state ->
            let maybe =
                tryParseOne state
            match maybe with
            | Some (item, loc, etc, doContinue) ->
                if doContinue then
                    Some ((item, loc, etc), etc) // notice each item includes the remainder, because we need (the last emitted remainder) at the end
                else
                    Some ((item, loc, etc), List.Empty)
            | None ->
                None)
    match rawParsed with
    | [] ->
        None
    | (_, loc, _) :: _ ->
        let parsed =
            rawParsed
            |> List.map (fun (thing, _, _) -> thing)
        let etc =
            rawParsed
            |> List.rev
            |> List.head
            |> (fun (_, _, etc) -> etc)
        Some (parsed, loc, etc)

type ContentUntilMatchingState = {
    LastLoc: Location
    NestLevel: int
    Remaining: TokenAndLocation list
}
    
let contentUntilMatching (loc: Location) (startToken: Token) (input: TokenAndLocation list) =
    // return content, etc (excluding the end token)
    let endToken =
        match startToken with
        | LeftParen -> RightParen
        | LeftAngleBracket -> RightAngleBracket
        | LeftBrace -> RightBrace
        | _ -> failwith "contentUntilMatching: unknown opening token"
    // find the first instance of endToken that's not balanced by another startToken
    let content =
        {LastLoc = loc; NestLevel = 0; Remaining = input}
        |> List.unfold (fun acc ->
            match acc.Remaining with
            | h :: etc ->
                let nextLastLoc =
                    h.Location
                if h.Token = endToken then
                    if acc.NestLevel = 0 then
                        None
                    else
                        Some(h, { LastLoc = nextLastLoc; NestLevel = acc.NestLevel - 1; Remaining = etc })
                elif h.Token = startToken then
                    Some(h, { LastLoc = nextLastLoc; NestLevel = acc.NestLevel + 1; Remaining = etc })
                else
                    Some(h, { acc with LastLoc = nextLastLoc; Remaining = etc })
            | [] ->
                failwithf "failed to find matching %A at %s" endToken (acc.LastLoc.ToString()))
    let _, etc =
        input
        |> List.splitAt (content.Length + 1) // skip the closing <whatever>
    content, etc

module NameStuff =
    let (|NameParts|_|) (input: TokenAndLocation list) =
        let tryParseOne = function
            | Lookahead 1 ([Identifier id], loc, etc) ->
                match etc with
                | Lookahead 1 ([Dot], loc, etc) ->
                    // got item, continue
                    Some (id, loc, etc, true)
                | _ ->
                    // got item, was end
                    Some (id, loc, etc, false)
            | _ ->
                // no item
                None
        whileDoesParse tryParseOne input
    let parseList (delimiter: Token) (endToken: Token) (loc: Location) (input: TokenAndLocation list) =
        // eg ns1.ns2.name, ns1.name, name, ns3.ns4.name;
        let tryParseOne = function
            | NameParts (parts, loc, etc) ->
                match etc with
                | Lookahead 1 ([delim], loc, etc) when delim = delimiter -> // more to come
                    Some (parts, loc, etc, true)
                | _ -> // last one (will check for endToken later)
                    Some (parts, loc, etc, false)
            | _ ->
                None
        match whileDoesParse tryParseOne input with
        | Some (names, loc, etc) ->
            // verify endToken in place
            match etc with
            | Lookahead 1 ([t], loc, etc) when t = endToken ->
                names, loc, etc
            | _ ->
                failwithf "NameStuff.parseList: missing required terminator @ %A" loc
        | _ ->
            failwithf "NameStuff.parseList: expected name list @ %A" loc

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

type NamedFunction = {
    Name: string
    Args: (string * NIType) list
    Returns: NIType
    Throws: NIType list
}

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

[<RequireQualifiedAccess>]
type EnumValue =
    | Decimal of value: int64 * leftShift: int
    | Hex of value: int64 * leftShift: int
    
type EnumInfo = {
    Values: (string * EnumValue option) list // name, specified value
    MaybeWidth: NIType option
    FlagsMode: bool
}

let isValidEnumWidth (t: NIType) =
    match t with
    | Primitive (Int _) -> true
    | _ -> false

type OpaqueInfo = {
    MaybeParent: NIType option          // must be an opaque, obviously
    Methods: NamedFunction list
    StaticDefs: ModuleDefinition list   // ie, just stuff namespace-prefixed by the opaque type
}
and UserType =
    | OpaqueDef of OpaqueInfo
    | StructDef of fields: NamedField list
    | ExceptionDef of fields: NamedField list      // created as an args list, although they are accessible fields
    | InterfaceDef of methods: NamedFunction list
    | DelegateDef of info: DelegateInfo
    | TypeDef of actual: NIType
    | EnumDef of info: EnumInfo
    | SumTypeDef of values: SumTypeValue list
    | OptionsDef of fields: NamedField list
and ModuleDefinition =
    | Global of name: string * type_: NIType
    | FunctionDef of NamedFunction
    | UserType of name: string * type_: UserType

type SymbolTableEntry =
    | Direct of thing: UserType
    | SubTable of maybeThing: UserType option * table: SymbolTable
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

let parseArg (input: TokenAndLocation list) =
    if input.IsEmpty then
        None
    else
        let type_, etc =
            parseType input
        // printfn "type_ %A" type_
        // printfn "etc: %A" (etc |> List.safeTake 20)
        match etc with
        | Lookahead 2 ([Identifier name; Comma], _, etc2) ->
            Some ((name, type_), etc2)
        | [{ Location = _; Token = Identifier name }] -> // TODO: fix lookahead to use N as 'max', so it returns UP TO that many?
            Some ((name, type_), [])
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
                Some ({ Name = funcName; Args = args; Returns = returnType; Throws = List.empty }, etc)
            | Lookahead 1 ([ThrowsToken], loc, etc) ->
                let names, loc, etc =
                    NameStuff.parseList Comma Semicolon loc etc
                let throwsList =
                    names
                    |> List.map (fun name -> Unresolved (UnresolvedIdentifier name, loc, ExceptionConstraint))
                Some ({ Name = funcName; Args = args; Returns = returnType; Throws = throwsList }, etc)
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
    | Lookahead 1 ([Identifier name], loc, etc) ->
        // optional user-specified numeric value?
        let value, etc =
            match etc with
            | Lookahead 2 ([EqualsToken; Digits valueDigits], _, etc) ->
                let value = int64 valueDigits
                match etc with
                | Lookahead 2 ([LeftShiftToken; Digits shiftDigits], _, etc) ->
                    // shifted value
                    let shift = int32 shiftDigits
                    (name, EnumValue.Decimal (value, shift) |> Some), etc
                | _ ->
                    // unshifted direct value
                    (name, EnumValue.Decimal (value, 0) |> Some), etc
            | Lookahead 2 ([EqualsToken; HexDigits valueDigits], _, etc) ->
                let value = Convert.ToInt64(valueDigits, 16)
                match etc with
                | Lookahead 2 ([LeftShiftToken; Digits shiftDigits], _, etc) ->
                    // shifted value
                    let shift = int32 shiftDigits
                    (name, EnumValue.Hex (value, shift) |> Some), etc
                | _ ->
                    // unshifted direct value
                    (name, EnumValue.Hex (value, 0) |> Some), etc
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
        // will there be more?
        match etc2 with
        | Lookahead 1 ([Comma], _, etc3) ->
            Some ({ Name = name; Args = args }, etc3)
        | [] ->
            // we're done, no more tokens
            Some ({ Name = name; Args = args }, []) // no etc, unfold will stop
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
    
let rec parseModuleDefs (input: TokenAndLocation list) =
    let parseNext (remaining: TokenAndLocation list) =
        match remaining with
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
            | Lookahead 1 ([Semicolon], loc, etc) ->
                let info =
                    { MaybeParent = maybeParent; Methods = []; StaticDefs = [] }
                ([UserType (opaqueName, OpaqueDef info)], etc)
                |> Some
            // opaque with content (methods + optional static {} block)
            | Lookahead 1 ([LeftBrace], loc, etc) ->
                // first, figure out the limits of the opaque content block as a whole
                let contentBlock, afterBlock =
                    contentUntilMatching loc LeftBrace etc
                    
                // split just before "static {", if any
                let staticBlockMatcher = function
                    | Lookahead 2 ([StaticToken; LeftBrace], _, _) -> true
                    | _ -> false
                let methodsTokens, afterMethods =
                    match splitTokensBefore staticBlockMatcher contentBlock with
                    | Some (before, after) ->
                        before, after
                    | None ->
                        contentBlock, []
                        
                // parse the methods-only part
                let methods =
                    methodsTokens
                    |> List.unfold parseNamedFunction
                
                // is there a static {} block?
                let staticDefs =
                    match afterMethods with
                    | [] ->
                        []
                    | Lookahead 2 ([StaticToken; LeftBrace], loc, etc) ->
                        let staticBlock, _ =
                            contentUntilMatching loc LeftBrace etc
                        // woot!
                        parseModuleDefs staticBlock
                    | { Location = loc } :: _ ->
                        failwithf "unknown content after opaque methods @ %s" (loc.ToString())
                let info =
                    { MaybeParent = maybeParent; Methods = methods; StaticDefs = staticDefs }
                ([UserType (opaqueName, OpaqueDef info)], afterBlock)
                |> Some
            | _ ->
                failwithf "error parsing opaque token at %s" (loc.ToString())
        | Lookahead 3 ([StructToken; Identifier name; LeftBrace], loc, etc) ->
            let fields, etc2 =
                parseFields loc etc
            ([UserType (name, StructDef fields)], etc2)
            |> Some
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
                args |> List.map (fun (name, type_) -> { Name = name; Type_ = type_ })
            ([UserType (name, ExceptionDef fields)], etc)
            |> Some
        | Lookahead 3 ([InterfaceToken; Identifier name; LeftBrace], loc, etc) ->
            let methods, etc2 =
                parseFunctionList loc etc
            ([UserType (name, InterfaceDef methods)], etc2)
            |> Some
        | Lookahead 1 ([DelegateToken], _, etc) ->
            parseNamedFunction etc
            |> Option.map (fun (nf, etc2) ->
                let result =
                    UserType (nf.Name, DelegateDef { Args = nf.Args; Returns = nf.Returns; Throws = nf.Throws })
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
    List.unfold parseNext input
    |> List.collect id

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
                | [], Direct thing ->
                    // direct hit, OK
                    Some thing
                | [], SubTable(maybeThing, _) ->
                    // maybe direct hit, depends if there was an enclosing entity of the subtable
                    maybeThing
                | more, Direct thing ->
                    // had more name parts, but found (the wrong thing) preventing deeper search
                    None
                | more, SubTable(maybeThing, subTable) ->
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
                | _, SubTable(maybeThing, subTable) ->
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

type ImportResult = {
    ImportName: ModuleName
    Result: string list * UserType
}

let resolveIdentifier (context: ModuleContext) (loc: Location) (namespacePath: string list) (UnresolvedIdentifier nameParts) =
    // 1. try to find in the current module
    match findModuleSymbol context.Module namespacePath nameParts with
    | Some (nsPrefix, thing) ->
        let symbolName =
            SymbolName (nsPrefix @ nameParts)
        (ThisModule symbolName, thing)
    | None ->
        // 2. not in this module, what about imports?
        let importResults =
            context.Module.Imports
            |> List.choose (fun importName ->
                let import =
                    context.Loaded[importName]
                // TODO: strip off any common prefix between thing and full module path!!
                findModuleSymbol import [] nameParts
                |> Option.map (fun res ->
                    { ImportName = importName; Result = res }))
        match importResults with
        | _ :: _ :: _ ->
            // 2+ results, ambiguous = not OK
            let matchedNames =
                importResults
                |> List.map (fun { ImportName = name } -> name)
            failwithf "resolveIdentifier: identifier %A in module %A matched symbols in multiple imports %A @ %A" nameParts context.Module.Name matchedNames loc
        | [ { ImportName = importName; Result = (nsPrefix, thing) } ] ->
            // single result, OK
            let symbolName =
                SymbolName (nsPrefix @ nameParts)
            (Imported (importName, symbolName), thing)
        | [] ->
            // not found at all!
            failwithf "resolveIdentifier: identifier %A not found in containing module %A nor in any imports @ %A" nameParts context.Module.Name loc

let rec resolveType (context: ModuleContext) (namespacePath: string list) (type_: NIType) =
    match type_ with
    | ArrayType niType ->
        ArrayType (resolveType context namespacePath niType)
    | MapType (keyType, valueType) ->
        MapType (resolveType context namespacePath keyType, resolveType context namespacePath valueType)
    | TupleType tupleTypes ->
        TupleType (tupleTypes |> List.map (resolveType context namespacePath))
    | SetType niType ->
        SetType (resolveType context namespacePath niType)
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
            | ExceptionConstraint ->
                match resolvedDef with
                | ExceptionDef _ ->
                    () // OK
                | _ ->
                    failwithf "type constraint failed @ %A - must be 'exception' type" loc
            let resolvedType =
                match resolvedDef with
                | OpaqueDef _ -> OpaqueType
                | StructDef _ -> StructType
                | InterfaceDef _ -> InterfaceType
                | DelegateDef _ -> DelegateType
                | TypeDef _ -> TypedefType
                | EnumDef _ -> EnumType
                | SumTypeDef _ -> SumType
                | OptionsDef _ -> OptionsType
                | ExceptionDef _ -> ExceptionType
            Resolved (resolvedName, resolvedType)
    | _ ->
        type_

let resolveFieldType (context: ModuleContext) (namespacePath: string list) (ft: NamedField) =
    let nextType =
        resolveType context namespacePath ft.Type_
    { ft with Type_ = nextType }

let resolveFunctionTypes (context: ModuleContext) (namespacePath: string list) (nf: NamedFunction) =
    let nextArgs =
        nf.Args
        |> List.map (fun (name, argType) -> (name, resolveType context namespacePath argType))
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
    | UserType (name, userType) ->
        let nextUserType =
            match userType with
            | StructDef fieldTypes ->
                let nextFieldTypes =
                    fieldTypes
                    |> List.map (resolveFieldType context namespacePath)
                StructDef nextFieldTypes
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
            | InterfaceDef namedFunctions ->
                let nextFunctions =
                    namedFunctions
                    |> List.map (resolveFunctionTypes context namespacePath)
                InterfaceDef nextFunctions
            | DelegateDef delegateInfo ->
                let nextInfo =
                    resolveDelegateTypes context namespacePath delegateInfo
                DelegateDef nextInfo
            | OpaqueDef info ->
                // if there's a static block, immediately push a deeper namespace in which to potentially evaluate
                // (that way the containing opaque {} block can refer to anything in its static {} block directly - enums, structs, etc)
                let subPath =
                    match info.StaticDefs with
                    | [] -> namespacePath       // no content, just keep using same NS
                    | _ -> name :: namespacePath
                let nextMaybeParent =
                    info.MaybeParent
                    |> Option.map (resolveType context namespacePath) // no point using subpath here, we can't inherit from something we define!
                let nextMethods =
                    info.Methods
                    |> List.map (resolveFunctionTypes context subPath)
                let nextStaticDefs =
                    info.StaticDefs
                    |> List.map (resolveModuleDef context subPath)
                let nextInfo =
                    { MaybeParent = nextMaybeParent; Methods = nextMethods; StaticDefs = nextStaticDefs }
                OpaqueDef nextInfo
            | TypeDef niType ->
                TypeDef (resolveType context namespacePath niType)
            | EnumDef values ->
                EnumDef values
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
        | UserType (name, definition) ->
            match definition with
            // currently only opaque type supports static block
            | OpaqueDef opaqueInfo ->
                match opaqueInfo.StaticDefs with
                | [] ->
                    Some (name, Direct definition)
                | _ ->
                    let subTable =
                        getUserTypes opaqueInfo.StaticDefs
                    Some (name, SubTable (Some definition, subTable))
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
        String.Join("/", nameParts).ToLower()
    $"{rootPath}/{pathParts}.nimpl"

let rec loadModule (rootPath: string) (moduleMap: Map<ModuleName, Module>) (name: ModuleName) =
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
            (moduleMap, parsed.Imports)
            ||> List.fold (fun acc name -> loadModule rootPath acc name |> snd)
        // perform resolution against our imports
        let resolved =
            parsed
            |> resolveUserTypes nextMap
        // return updated module map
        resolved, nextMap.Add(name, resolved)
