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
    | UnitToken
    | DelegateToken
    | TypedefToken
    | BufferToken
    | ThrowsToken
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
    | EqualsToken
    | LeftShiftToken
    
type Location = {
    Line: int
    Column: int
} with
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
        
let (|Comment|_|) (input: char[]) = // returns (+cols, +lines, etc)
    if input.Length >= 2 then
        let first, etc =
            input |> Array.splitAt 2
        match first with
        | [|'/';'/'|] ->
            // go until end of line (or end of input!)
            match etc |> Array.tryFindIndex (fun ch -> ch = '\n') with
            | Some i ->
                Some (2 + i, 0, etc |> Array.skip i)
            | None ->
                // no newline, goodbye rest of input!
                Some (2 + etc.Length, 0, [||])
//        | [|'/'; '*'|] ->
//            // inline comment, potentially multi-line
        | _ ->
            None
    else
        None
        
let isNewLine = function
    | '\n' | '\r' -> true
    | _ -> false
    
let isWhitespace = function
    | ' ' | '\t' -> true
    | _ -> false
    
let isAlphaNumeric ch =
    (ch >= 'a' && ch <= 'z') ||
    (ch >= 'A' && ch <= 'Z') ||
    (ch >= '0' && ch <= '9') ||
    ch = '_'
    
let allowedSymbols =
    ["{"; "}"; "("; ")"; "<<"; "<"; ">"; "="; ","; ":"; ";"; "->"]
    
let nextToken (loc: Location) (input: char[]) =
    match input with
    | WhileMatch isNewLine (_match, _, etc) ->
        None, etc, -loc.Column + 1, 1                   // reset column (to 1), advance line
    | WhileMatch isWhitespace (_match, cols, etc) ->
        None, etc, cols, 0
    | Comment (cols, lines, etc) ->
        None, etc, cols, lines
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
            | _ -> failwithf "unrecognized symbol: %s at line %d, col %d" _match loc.Line loc.Column
        Some token, etc, cols, 0
    | WhileMatch isAlphaNumeric (_match, cols, etc) ->
        // number, identifier, reserved word
        // either a token or a failure!
        let token =
            match String(_match) with
            | "module" -> ModuleToken
            | "opaque" -> OpaqueToken
            | "struct" -> StructToken
            | "exception" -> ExceptionToken
            | "interface" -> InterfaceToken
            | "enum" -> EnumToken
            | "flags" -> FlagsToken
            | "sumtype" -> SumTypeToken
            | "delegate" -> DelegateToken
            | "typedef" -> TypedefToken
            | "throws" -> ThrowsToken
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
        Some token, etc, cols, 0
    | [||] ->
        None, [||], 0, 0
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
            let maybeToken, etc, colAdvance, lineAdvance =
                nextToken state.Loc state.Remaining
            let nextState =
                let nextLoc =
                    { state.Loc with
                        Line = state.Loc.Line + lineAdvance
                        Column = state.Loc.Column + colAdvance }
                { Loc = nextLoc; Remaining = etc }
            let value =
                maybeToken
                |> Option.map (fun token -> { Token = token; Location = state.Loc })
            Some (value, nextState))
    |> List.choose id
