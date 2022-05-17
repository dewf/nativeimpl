module Tokenizer

open System
open System.Text.RegularExpressions

type Location = {
    Line: int
    Column: int
}

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
    | LeftBrace
    | RightBrace
    | LeftParen
    | RightParen
    | LeftAngleBracket
    | RightAngleBracket
    | StatementEnd
    | Comma
    | Colon
    | EqualsToken
    | LeftShiftToken
    | FunctionArrow    // not currently used, just for testing
    | ThrowsToken
    | Error of line: int * text: string

let stringToToken (lineNumber: int) = function
    | "module" -> ModuleToken
    | "opaque" -> OpaqueToken
    | "struct" -> StructToken
    // generic types (need specialization)
    | "Array" -> ArrayToken
    | "Map" -> MapToken
    | "Tuple" -> TupleToken
    | "Set" -> SetToken
    | "Buffer" -> BufferToken
    //
    | "exception" -> ExceptionToken
    | "interface" -> InterfaceToken
    | "enum" -> EnumToken
    | "flags" -> FlagsToken
    | "sumtype" -> SumTypeToken
    | "size_t" -> SizeTToken
    | "bool" -> BoolToken
    //
    | "sbyte" | "char" | "int8" -> IntToken (true, 8)
    | "byte" | "uchar" | "uint8" -> IntToken (false, 8)
    | "short" | "int16" -> IntToken (true, 16)
    | "ushort" | "uint16" -> IntToken (false, 16)
    | "int" | "int32" -> IntToken (true, 32)
    | "uint" | "uint32" -> IntToken (false, 32)
    | "long" | "int64" -> IntToken (true, 64)
    | "ulong" | "uint64" -> IntToken (false, 64)
    //
    | "float" | "float32" -> Float32Token
    | "double" | "float64" -> Float64Token
    | "string" -> StringToken
    //
    | "void" -> UnitToken
    | "unit" -> UnitToken
    | "delegate" -> DelegateToken
    | "typedef" -> TypedefToken
    | "{" -> LeftBrace
    | "}" -> RightBrace
    | "(" -> LeftParen
    | ")" -> RightParen
    | "<" -> LeftAngleBracket
    | ">" -> RightAngleBracket
    | ";" -> StatementEnd
    | "," -> Comma
    | ":" -> Colon
    | "=" -> EqualsToken
    | "<<" -> LeftShiftToken
    | "->" -> FunctionArrow
    | "throws" -> ThrowsToken
    | text when Regex(@"[_a-zA-Z][_a-zA-Z0-9]*").IsMatch(text) -> Identifier text
    | text when Regex(@"[0-9]+").IsMatch(text) -> Digits text
    | text -> Error (lineNumber, text)

type TokenAndLocation = {
    Token: Token
    Location: int
}

let tokenize (lines: (int * string) list) =
    lines
    |> List.filter (fun (_, line) -> line <> "")
    |> List.map (fun (i, line) ->
        let noComments = Regex(@"//.*").Replace(line, "")
        let spacesCollapsed = Regex(@"\s+").Replace(noComments, " ")
        let spaceAdded = Regex(@"[,\(\)\[\];{}<>:=]").Replace(spacesCollapsed, MatchEvaluator(fun m -> sprintf " %s " m.Value))
        let items = spaceAdded.Split(' ')
        let nonEmpty =
            items
            |> Array.filter (fun s -> s.Length > 0)
        i, nonEmpty |> List.ofArray)
    |> List.collect (fun (i, items) -> [for item in items -> { Token = stringToToken i item; Location = i } ])

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
//    if input.Length >= against.Length then
//        let input' =
//            Array.sub input 0 against.Length
//        if against = input' then
//            Some (against.Length, input |> Array.skip against.Length)
//        else
//            None
//    else
//        None
        
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
            // go until end of line (or end of input)
            match etc |> Array.tryFindIndex (fun ch -> ch = '\n') with
            | Some i ->
                Some (2 + i, 0, etc |> Array.skip i)
            | None ->
                // no newline, goodbye rest of input!
                Some (2 + etc.Length, 0, [||])
        // todo: C-style comments
        | _ ->
            None
    else
        None
        
//let (|WhileMatchInit|_|) (initPred: char -> bool) (pred: char -> bool) (input: char[]) =
//    if input.Length >= 1 && initPred input[0] then
//        let front, etc =
//            charsWhile pred (input |> Array.skip 1)
//        let front' =
//            seq { [|input[0]|]; front }
//            |> Array.concat
//        Some (front', front'.Length, etc)
//    else
//        None

let isWhitespace = function
    | ' ' | '\t' -> true
    | _ -> false
//let isDigitInit ch =
//    ch >= '1' && ch <= '9'
//let isDigit ch =
//    ch >= '0' && ch <= '9'
//let isAlphaNumericInit ch =
//    (ch >= 'a' && ch <= 'z') ||
//    (ch >= 'A' && ch <= 'A')
let isAlphaNumeric ch =
    (ch >= 'a' && ch <= 'z') ||
    (ch >= 'A' && ch <= 'Z') ||
    (ch >= '0' && ch <= '9') ||
    ch = '_'
    
let allowedSymbols =
    ["{"; "}"; "("; ")"; "<<"; "<"; ">"; "="; ";"; "->"]
    
let nextToken (loc: Location) (input: char[]) =
    match input with
    | Matches [|'\n'|] (_, etc) ->
        None, etc, -loc.Column, 1
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
            | ";" -> StatementEnd
            | "->" -> FunctionArrow
            | _ -> failwithf "unrecognized symbol: %s at line %d, col %d" _match loc.Line loc.Column
        Some token, etc, cols, 0
    | WhileMatch isAlphaNumeric (_match, cols, etc) ->
        let str =
            String(_match)
        if Regex(@"^[_a-zA-Z][_a-zA-Z0-9]*$").IsMatch(str) then
            Some (Identifier str), etc, cols, 0
        elif Regex(@"^[0-9]+$").IsMatch(str) then
            Some (Digits str), etc, cols, 0
        else
            failwithf "invalid number '%s' at line %d, col %d" str loc.Line loc.Column
    | [||] ->
        None, [||], 0, 0
    | _ ->
        failwithf "unrecognized character '%c'... at location (line: %d, col: %d)" input[0] loc.Line loc.Column
        
type TokenizerState = {
    Loc: Location
    Remaining: char[]
}

type TokenAndLocation2 = {
    Tok: Token
    Loc: Location
}

let tokenize2 (input: string) =
    let initState =
        { Loc = { Line = 1; Column = 1; }; Remaining = input.ToCharArray() }
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
                |> Option.map (fun token -> token, state.Loc)
            Some (value, nextState))
    |> List.choose id
