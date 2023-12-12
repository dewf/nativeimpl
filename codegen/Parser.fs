module Parser
open System.IO
open Tokenizer
open Util
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

type NIType =
    | UnitType
    | Primitive of prim: NIPrimitive
    // maybe these should be Generic of ...
    | ArrayType of elem: NIType
    | MapType of keyType: NIType * valueType: NIType
    | TupleType of tupleTypes: NIType list
    | SetType of elem: NIType
    | BufferType of elemType: NIType
    // Named of ...
    | Unresolved of name: string * loc: Location
    // these have to be resolved by checking a table, otherwise unresolved
    | OpaqueHandleType of name: string
    | StructType of name: string
    | InterfaceType of name: string
    | ExceptionType of name: string          // only for 'throws'
    | DelegateType of name: string
    | TypedefType of name: string
    | EnumType of name: string
    | SumType of name: string

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
            front |> List.map (fun tl -> tl.Token)
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
            front |> List.map (fun tl -> tl.Token)
        Some (tokens, h.Location, etc)
    | _ ->
        None

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
                        Some(h, { acc with LastLoc = nextLastLoc; NestLevel = acc.NestLevel - 1; Remaining = etc })
                elif h.Token = startToken then
                    Some(h, { acc with LastLoc = nextLastLoc; NestLevel = acc.NestLevel + 1; Remaining = etc })
                else
                    Some(h, { acc with LastLoc = nextLastLoc; Remaining = etc })
            | [] ->
                failwithf "failed to find matching %A at %s" endToken (acc.LastLoc.ToString()))
    let _, etc =
        input
        |> List.splitAt (content.Length + 1) // skip the closing <whatever>
    content, etc

let rec parseType (input: TokenAndLocation list) =
    match input with
    | Lookahead 1 ([Identifier name], loc, etc) -> // opaque handle, struct, interface
        Unresolved (name, loc), etc
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
            failwithf "failed to parse array type at %s" (loc.ToString())
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
                failwithf "missing ] for array type at %s" (loc.ToString())
        | _ ->
            failwithf "missing comma for array type at %s" (loc.ToString())
    | Lookahead 2 ([BufferToken; LeftAngleBracket], loc, etc) ->
        let elemType, etc =
            parseType etc
        if not (validBufferElementType elemType) then
            failwithf "incorrect buffer element type [%A] at %s (must be primitive type)" elemType (loc.ToString())
        match etc with
        | Lookahead 1 ([RightAngleBracket], _, etc) ->
            BufferType elemType, etc
        | _ ->
            failwithf "failed to parse buffer type at %s" (loc.ToString())
    | Lookahead 2 ([SetToken; LeftAngleBracket], loc, etc) ->
        let elemType, etc =
            parseType etc
        match etc with
        | Lookahead 1 ([RightAngleBracket], _, etc) ->
            SetType elemType, etc
        | _ ->
            failwithf "expected closing > at %s" (loc.ToString())
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
                        failwithf "parsing tuple: unexpected input at %s" (loc.ToString()))
        TupleType typeList, etc
    | [] ->
        failwithf "parseType called on an empty list!"
    | {Location = loc; Token = token} :: _ ->
        failwithf "unexpected token %A at %s" token (loc.ToString())

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

type EnumInfo = {
    Values: (string * int64 option) list // name, specified value
    MaybeWidth: NIType option
    FlagsMode: bool
}

let isValidEnumWidth (t: NIType) =
    match t with
    | Primitive (Int _) -> true
    | _ -> false

type UserType =
    | OpaqueHandleDef
    | StructDef of fields: NamedField list
    | ExceptionDef of fields: NamedField list      // created as an args list, although they are accessible fields
    | InterfaceDef of methods: NamedFunction list
    | DelegateDef of info: DelegateInfo
    | TypeDef of actual: NIType
    | EnumDef of info: EnumInfo
    | SumTypeDef of values: SumTypeValue list

type ModuleDefinition =
    | Global of name: string * type_: NIType
    | FunctionDef of NamedFunction
    | UserType of name: string * type_: UserType

type Module = {
    Name: string
    Imports: string list
    UserTypes: Map<string, UserType> option
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

let parseIdentifierList (loc: Location) (input: TokenAndLocation list) (endToken: Token) =
    let parseOneIdentifier (input': TokenAndLocation list) =
        if input'.IsEmpty then
            None
        else
            match input' with
            | Lookahead 2 ([Identifier name; Comma], _, etc) ->
                Some (name, etc)
            | [{Location = _; Token = Identifier name}] ->
                Some (name, List.empty)
            | _ ->
                failwithf "parseIdentifierList: expected identifier at %s (%A)" (loc.ToString()) input'
    let maybeEndIndex =
        input |> List.tryFindIndex (fun tl -> tl.Token = endToken)
    match maybeEndIndex with
    | Some index ->
        let idTokens, etc =
            input |> List.splitAt index
        let identifiers =
            idTokens |> List.unfold parseOneIdentifier
        match etc with
        | Lookahead 1 ([token], _, etc) when token = endToken ->
            identifiers, etc
        | _ ->
            failwithf "expected end of identifiers at %s" (loc.ToString())
    | None ->
        failwithf "expected end of identifiers at %s" (loc.ToString())

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
                let identifiers, etc =
                    parseIdentifierList loc etc Semicolon
                let throwsList =
                    identifiers |> List.map (fun name -> (Unresolved (name, loc)))
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
        List.unfold parseNamedFunction content
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
                    (name, (value <<< shift) |> Some), etc
                | _ ->
                    // unshifted direct value
                    (name, value |> Some), etc
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

let parseModuleDefs (input: TokenAndLocation list) =
    let parseNext (remaining: TokenAndLocation list) =
        match remaining with
        | Lookahead 2 ([OpaqueToken; Identifier opaqueName], loc, etc) ->
            match etc with
            | Lookahead 1 ([Semicolon], _, etc) ->
                ([UserType (opaqueName, OpaqueHandleDef)], etc)
                |> Some
            | Lookahead 1 ([LeftBrace], loc, etc) ->
                // opaque with "methods" (expand to OOP-esque functions)
                let methods, etc =
                    parseFunctionList loc etc
                let thisName =
                    "_this" //(upperOnly opaqueName).ToLowerInvariant()
                let functions =
                    methods
                    |> List.map (fun nf ->
                        // prefix the function name + add a 1st argument for 'this'
                        let nextName =
                            sprintf "%s%s" (lowerFirst opaqueName) (upperFirst nf.Name)
                        let nextArgs =
                            (thisName, Unresolved (opaqueName, loc)) :: nf.Args
                        FunctionDef { nf with Name = nextName; Args = nextArgs })
                let results =
                    UserType (opaqueName, OpaqueHandleDef) :: functions
                (results, etc)
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

let moduleIdentifierTokenFilter = function
    | Identifier _ | Dot -> true
    | _ -> false
    
let moduleNamePartsToString (parts: Token list) =
    parts
    |> List.map (function
        | Identifier id -> id
        | Dot -> "."
        | _ -> failwith "moduleNamePartsToString: shouldn't happen")
    
let consumeImports (input: TokenAndLocation list) =
    let rec nextImport (revImports: string list) (remaining: TokenAndLocation list) =
        match remaining with
        | Lookahead 1 ([ImportToken], loc, etc) ->
            match etc with
            | TakeWhile moduleIdentifierTokenFilter (nameParts, loc, etc) ->
                match etc with
                | Lookahead 1 ([Semicolon], _, etc) ->
                    // OK!
                    let joinedName =
                        moduleNamePartsToString nameParts
                        |> String.concat ""
                    nextImport (joinedName :: revImports) etc
                | _ ->
                    failwithf "expected semicolon after module identifier @ %s" (loc.ToString())
            | _ ->
                failwithf "expected module name identifier @ %s" (loc.ToString())
        | _ ->
            revImports, remaining
    let rev, etc =
        nextImport [] input
    List.rev rev, etc
    
let parseModuleToplevel (input: TokenAndLocation list) (outerName: string) =
    match input with
    | Lookahead 1 ([ModuleToken], loc, etc) ->
        match etc with
        | TakeWhile moduleIdentifierTokenFilter (nameParts, loc, etc) ->
            match etc with
            | Lookahead 1 ([Semicolon], _, etc) ->
                // OK!
                let joinedName =
                    moduleNamePartsToString nameParts
                    |> String.concat ""
                if joinedName <> outerName then
                    failwithf "internal module name [%s] did not match outer name/location [%s]" joinedName outerName
                // imports, if any must occur at top of file after "module"
                let imports, etc =
                    consumeImports etc
                { Name = joinedName
                  Imports = imports
                  UserTypes = None
                  Defs = parseModuleDefs etc }
            | _ ->
                failwithf "expected semicolon after module definition @ %s" (loc.ToString())
        | _ ->
            failwithf "expected module name identifier @ %s" (loc.ToString())
    | _ ->
        failwith "parseModuleToplevel: expected module definition at beginning of file"

// resolution stuff ===========================
type ModuleContext = {
    Loaded: Map<string, Module>
    Module: Module
}

// this is going to add a bunch of complexity though it will probably be a good idea eventually
// (for resolved NITypes, which currently just have a 'name' string)
// type ResolvedName =
//     | ThisModule of name: string
//     | Imported of path: string list * name: string

let resolveIdentifier (context: ModuleContext) (identifier: string) (loc: Location) =
    let toSearch =
        let fromImports =
            context.Module.Imports
            |> List.map (context.Loaded.TryFind >> Option.get >> (fun m -> m.UserTypes))
        context.Module.UserTypes :: fromImports
        |> List.map Option.get // because these should all have their symbol tables loaded by now
    let found =
        toSearch
        |> List.choose (fun map -> map.TryFind identifier)
    // must be only 1 result, otherwise it's ambiguous and we have a problem
    match found with
    | result :: etc ->
        if etc.Length > 0 then
            failwithf "error resolving identifier [%s] from module [%s] @ %s: ambiguous results (also present in an import?)" identifier context.Module.Name (loc.ToString())
        else
            result
    | _ ->
        failwithf "error resolving identifier [%s] from module [%s] @ %s: symbol not found, in module or its imports" identifier context.Module.Name (loc.ToString())

let rec resolveType (context: ModuleContext) (type_: NIType) =
    match type_ with
    | ArrayType niType ->
        ArrayType (resolveType context niType)
    | MapType (keyType, valueType) ->
        MapType (resolveType context keyType, resolveType context valueType)
    | TupleType tupleTypes ->
        TupleType (tupleTypes |> List.map (resolveType context))
    | SetType niType ->
        SetType (resolveType context niType)
    | Unresolved (name, loc) ->
        match resolveIdentifier context name loc with
        | OpaqueHandleDef -> OpaqueHandleType name
        | StructDef _ -> StructType name
        | InterfaceDef _ -> InterfaceType name
        | DelegateDef _ -> DelegateType name
        | TypeDef _ -> TypedefType name
        | EnumDef _ -> EnumType name
        | SumTypeDef _ -> SumType name
        | ExceptionDef _ ->
            // how do we enforce that this is only allowed in a "throws" context?
            ExceptionType name
    | _ ->
        type_

let resolveFieldType (context: ModuleContext) (ft: NamedField) =
    let nextType =
        resolveType context ft.Type_
    { ft with Type_ = nextType }

let resolveFunctionTypes (context: ModuleContext) (nf: NamedFunction) =
    let nextArgs =
        nf.Args
        |> List.map (fun (name, argType) -> (name, resolveType context argType))
    let nextReturns =
        resolveType context nf.Returns
    let nextThrows =
        nf.Throws
        |> List.map (resolveType context)
    { nf with Args = nextArgs; Returns = nextReturns; Throws = nextThrows }

let resolveDelegateTypes (context: ModuleContext) (info: DelegateInfo) =
    let nextArgs =
        info.Args
        |> List.map (fun (name, argType) -> (name, resolveType context argType))
    let nextReturns =
        resolveType context info.Returns
    let nextThrows =
        info.Throws
        |> List.map (resolveType context)
    { Args = nextArgs; Returns = nextReturns; Throws = nextThrows }

let resolveSumTypeTypes (context: ModuleContext) (values: SumTypeValue list) =
    values
    |> List.map (fun value ->
        let nextArgs =
            value.Args
            |> List.map (fun (name, type_) ->
                name, resolveType context type_)
        { value with Args = nextArgs })

let resolveModuleDef (context: ModuleContext) (def: ModuleDefinition) =
    match def with
    | UserType (name, userType) ->
        let nextUserType =
            match userType with
            | StructDef fieldTypes ->
                let nextFieldTypes =
                    fieldTypes
                    |> List.map (resolveFieldType context)
                StructDef nextFieldTypes
            | ExceptionDef fieldTypes ->
                let nextFieldTypes =
                    fieldTypes
                    |> List.map (resolveFieldType context)
                ExceptionDef nextFieldTypes
            | InterfaceDef namedFunctions ->
                let nextFunctions =
                    namedFunctions
                    |> List.map (resolveFunctionTypes context)
                InterfaceDef nextFunctions
            | DelegateDef delegateInfo ->
                let nextInfo =
                    resolveDelegateTypes context delegateInfo
                DelegateDef nextInfo
            | OpaqueHandleDef ->
                OpaqueHandleDef
            | TypeDef niType ->
                TypeDef (resolveType context niType)
            | EnumDef values ->
                EnumDef values
            | SumTypeDef enumValues ->
                let nextValues =
                    resolveSumTypeTypes context enumValues
                SumTypeDef nextValues
        UserType (name, nextUserType)
    | Global (name, niType) ->
        Global (name, resolveType context niType)
    | FunctionDef namedFunction ->
        FunctionDef (resolveFunctionTypes context namedFunction)

let getUserTypes (module_: Module) =
    module_.Defs
    |> List.choose (function
        | UserType (name, definition) -> Some (name, definition)
        | _ -> None)
    |> Map.ofList

let resolveUserTypes (loaded: Map<string, Module>) (module_: Module) =
    let nextModule =
        let symbols =
            getUserTypes module_
        { module_ with UserTypes = Some symbols }
    let context =
        { Loaded = loaded; Module = nextModule }
    // go throughout whole tree and replace all Unresolved types with things from the table, if they even exist
    let nextDefs =
        nextModule.Defs
        |> List.map (resolveModuleDef context)
    // now has both a symbol table and resolved definitions
    { nextModule with Defs = nextDefs }
    
let moduleNameToFilename (rootPath: string) (name: string) =
    let parts =
        name.Replace(".", "/").ToLower()
    $"{rootPath}/{parts}.nimpl"

let rec loadModule (rootPath: string) (moduleMap: Map<string, Module>) (name: string) =
    match moduleMap.TryFind name with
    | Some m ->
        // already loaded!
        m, moduleMap
    | None ->
        printfn "loading [%s]" name
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
