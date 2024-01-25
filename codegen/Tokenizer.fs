module Tokenizer

open System
open System.Text.RegularExpressions

type Token =
    | Identifier of string
    | Digits of string
    // primitives
    | SizeTToken
    | BoolToken
    | IntToken of signed:bool * width:int
    | Float32Token
    | Float64Token
    | StringToken
    // etc
    | ModuleToken
    | ImportToken
    | OpaqueToken
    | StructToken
    | ArrayToken
    | MapToken
    | TupleToken
    | SetToken
    | ExceptionToken
    | InterfaceToken
    | EnumToken | FlagsToken
    | SumTypeToken
    | OptionsToken
    | UnitToken
    | DelegateToken
    | TypedefToken
    | BufferToken
    | ThrowsToken
    | StaticToken
    // symbols:
    | LeftBrace
    | RightBrace
    | LeftParen
    | RightParen
    | LeftAngleBracket
    | RightAngleBracket
    | Semicolon
    | Comma
    | Colon
    | Dot
    | EqualsToken
    | LeftShiftToken
    
type Location = {
    Line: int
    Column: int
} with
    member this.PlusCol(n: int) =
        { this with Column = this.Column + n }
    member this.PlusLine(n: int) =
        { this with Line = this.Line + n }
    override this.ToString() =
        sprintf "Line: %d, Column: %d" this.Line this.Column

type TokenAndLocation = {
    Token: Token
    Location: Location
}

let charsWhile (pred: char -> bool) (input: char[]) =
    let front =
        input
        |> Array.takeWhile pred
    if input.Length > front.Length then
        front, input |> Array.skip front.Length
    else
        front, [||]

let inline matchesAgainstInput (against: char[]) (input: char[]) =
    if input.Length >= against.Length then
        let input' =
            Array.sub input 0 against.Length
        if against = input' then
            Some (against.Length, input |> Array.skip against.Length)
        else
            None
    else
        None

let (|Matches|_|) (against: char[]) (input: char[]) =
    matchesAgainstInput against input
        
let (|AnyOf|_|) (items: string list) (input: char[]) =
    items
    |> Seq.map (fun str -> str, str.ToCharArray())
    |> Seq.choose (fun (str, chars) ->
        matchesAgainstInput chars input
        |> Option.map (fun (len, etc) -> str, len, etc))
    |> Seq.tryHead
        
let (|WhileMatch|_|) (pred: char -> bool) (input: char[]) =
    let front, etc =
        charsWhile pred input
    if front.Length > 0 then
        Some (front, front.Length, etc)
    else
        None
        
let (|SingleChar|_|) (input: char[]) =
    if input.Length >= 1 then
        Some (input[0], Array.skip 1 input)
    else
        None

type CommentState = {
    Complete: bool
    Depth: int
    Loc: Location
    Remaining: char[]
}

let unfoldComment (state: CommentState) =
    if state.Complete then
        None
    else
        match state.Remaining with
        | Matches [|'/';'*'|] (len, etc) ->
            Some { state with Depth = state.Depth + 1; Loc = state.Loc.PlusCol(len); Remaining = etc }
        | Matches [|'*';'/'|] (len, etc) ->
            if state.Depth = 1 then
                // finished
                Some { Complete = true; Depth = 0; Loc = state.Loc.PlusCol(len); Remaining = etc }
            else
                Some { state with Depth = state.Depth - 1; Loc = state.Loc.PlusCol(len); Remaining = etc }
        | Matches [|'\r'|] (_, etc) ->
            // just ignore, don't allow SingleChar default to handle it (location can't change)
            Some { state with Remaining = etc }
        | Matches [|'\n'|] (_, etc) ->
            let nextLoc =
                { Column = 1; Line = state.Loc.Line + 1 }
            Some { state with Loc = nextLoc; Remaining = etc }
        | SingleChar (_, etc) ->
            Some { state with Loc = state.Loc.PlusCol(1); Remaining = etc }
        | [||] ->
            Some { state with Complete = true }
        | _ ->
            failwith "nextComment: shouldn't see this"
        
let (|Comment|_|) (loc: Location) (input: char[]) = // returns (+cols, +lines, etc)
    if input.Length >= 2 then
        let first, etc =
            input |> Array.splitAt 2
        match first with
        | [|'/';'/'|] ->
            // go until end of line (or end of input!)
            match etc |> Array.tryFindIndex (fun ch -> ch = '\n') with
            | Some i ->
                let nextLoc =
                    { loc with Column = loc.Column + 2 + i }
                Some (nextLoc, etc |> Array.skip i)
            | None ->
                // no newline found, goodbye rest of input!
                let nextLoc =
                    { loc with Column = loc.Column + 2 + etc.Length }
                Some (nextLoc, [||])
        | [|'/'; '*'|] ->
            let res =
                { Complete = false; Depth = 1; Loc = loc; Remaining = etc }
                |> Util.unfoldState unfoldComment
            Some (res.Loc, res.Remaining)
        | _ ->
            None
    else
        None
        
let isWhitespace = function
    | ' ' | '\t' -> true
    | _ -> false
    
let isAlphaNumeric ch =
    (ch >= 'a' && ch <= 'z') ||
    (ch >= 'A' && ch <= 'Z') ||
    (ch >= '0' && ch <= '9') ||
    ch = '_'
    
let allowedSymbols =
    ["{"; "}"; "("; ")"; "<<"; "<"; ">"; "="; ","; ":"; ";"; "."; "->"]
    
let nextToken (loc: Location) (input: char[]) =
    match input with
    | Matches [|'\r'|] (_match, etc) ->
        // inert
        None, etc, loc
    | Matches [|'\n'|] (_match, etc) ->
        None, etc, { Column = 1; Line = loc.Line + 1 }
    | WhileMatch isWhitespace (_match, cols, etc) ->
        None, etc, loc.PlusCol(cols)
    | Comment loc (nextLoc, etc) ->
        None, etc, nextLoc
    | AnyOf allowedSymbols (_match, cols, etc) ->
        let token =
            match _match with
            | "{" -> LeftBrace
            | "}" -> RightBrace
            | "(" -> LeftParen
            | ")" -> RightParen
            | "<<" -> LeftShiftToken
            | "<" -> LeftAngleBracket
            | ">" -> RightAngleBracket
            | "=" -> EqualsToken
            | "," -> Comma
            | ":" -> Colon
            | ";" -> Semicolon
            | "." -> Dot
            | _ -> failwithf "unrecognized symbol: %s at line %d, col %d" _match loc.Line loc.Column
        Some token, etc, loc.PlusCol(cols)
    | WhileMatch isAlphaNumeric (_match, cols, etc) ->
        // number, identifier, reserved word
        // either a token or a failure!
        let token =
            match String(_match) with
            | "module" -> ModuleToken
            | "import" -> ImportToken
            | "opaque" -> OpaqueToken
            | "struct" -> StructToken
            | "exception" -> ExceptionToken
            | "interface" -> InterfaceToken
            | "enum" -> EnumToken
            | "flags" -> FlagsToken
            | "sumtype" -> SumTypeToken
            | "options" -> OptionsToken
            | "delegate" -> DelegateToken
            | "typedef" -> TypedefToken
            | "throws" -> ThrowsToken
            | "static" -> StaticToken
            // generic types (need specialization)
            | "Array" -> ArrayToken
            | "Map" -> MapToken
            | "Tuple" -> TupleToken
            | "Set" -> SetToken
            | "Buffer" -> BufferToken
            //
            | "void" -> UnitToken
            | "unit" -> UnitToken
            | "size_t" -> SizeTToken
            | "bool" -> BoolToken
            | "sbyte" | "char" | "int8" -> IntToken (true, 8)
            | "byte" | "uchar" | "uint8" -> IntToken (false, 8)
            | "short" | "int16" -> IntToken (true, 16)
            | "ushort" | "uint16" -> IntToken (false, 16)
            | "int" | "int32" -> IntToken (true, 32)
            | "uint" | "uint32" -> IntToken (false, 32)
            | "long" | "int64" -> IntToken (true, 64)
            | "ulong" | "uint64" -> IntToken (false, 64)
            | "float" | "float32" -> Float32Token
            | "double" | "float64" -> Float64Token
            | "string" -> StringToken
            | text when Regex(@"[_a-zA-Z][_a-zA-Z0-9]*").IsMatch(text) -> Identifier text
            | text when Regex(@"[0-9]+").IsMatch(text) -> Digits text
            | text ->
                failwithf "failed to parse string '%s' at %s" text (loc.ToString())
        Some token, etc, loc.PlusCol(cols)
    | [||] ->
        None, [||], loc
    | _ ->
        failwithf "unrecognized character '%s'... at location %s" (input[0].ToString()) (loc.ToString())
        
type TokenizerState = {
    Loc: Location
    Remaining: char[]
}

let tokenize (input: string) =
    let initState =
        let initLoc =
            { Line = 1; Column = 1; }
        { Loc = initLoc; Remaining = input.ToCharArray() }
    initState
    |> List.unfold (fun state ->
        if state.Remaining = [||] then
            None
        else
            let maybeToken, etc, nextLoc =
                nextToken state.Loc state.Remaining
            let nextState =
                { Loc = nextLoc; Remaining = etc }
            let value =
                maybeToken
                |> Option.map (fun token -> { Token = token; Location = state.Loc })
            Some (value, nextState))
    |> List.choose id
