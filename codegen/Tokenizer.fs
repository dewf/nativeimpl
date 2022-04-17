module Tokenizer

open System.Text.RegularExpressions

type Token =
    | Identifier of string
    | ModuleToken
    | OpaqueToken
    | StructToken
    | ArrayToken
    | MapToken
    | ExceptionToken
    | InterfaceToken
    | BoolToken
    | IntToken
    | FloatToken
    | StringToken
    | UnitToken
    | DelegateToken
    | LeftBrace
    | RightBrace
    | LeftParen
    | RightParen
    | LeftBracket
    | RightBracket
    | StatementEnd
    | Comma
    | ThrowsToken
    | Error of line: int * text: string

let stringToToken (lineNumber: int) = function
    | "module" -> ModuleToken
    | "opaque" -> OpaqueToken
    | "struct" -> StructToken
    | "array" -> ArrayToken
    | "map" -> MapToken
    | "exception" -> ExceptionToken
    | "interface" -> InterfaceToken
    | "bool" -> BoolToken
    | "int" | "int32" -> IntToken
    | "float" | "float32" -> FloatToken
    | "string" -> StringToken
    | "void" -> UnitToken
    | "unit" -> UnitToken
    | "delegate" -> DelegateToken
    | "{" -> LeftBrace
    | "}" -> RightBrace
    | "(" -> LeftParen
    | ")" -> RightParen
    | "[" -> LeftBracket
    | "]" -> RightBracket
    | ";" -> StatementEnd
    | "," -> Comma
    | "throws" -> ThrowsToken
    | text when Regex(@"[_a-zA-Z][_a-zA-Z0-9]*").IsMatch(text) -> Identifier text
    | text -> Error (lineNumber, text)

type TokenAndLocation = {
    Token: Token
    Location: int  // just line number for now
}

let tokenize (lines: (int * string) list) =
    lines
    |> List.filter (fun (_, line) -> line <> "")
    |> List.map (fun (i, line) ->
        let noComments = Regex(@"//.*").Replace(line, "")
        let spacesCollapsed = Regex(@"\s+").Replace(noComments, " ")
        let spaceAdded = Regex(@"[,\(\)\[\];{}]").Replace(spacesCollapsed, MatchEvaluator(fun m -> sprintf " %s " m.Value))
        let items = spaceAdded.Split(' ')
        let nonEmpty =
            items
            |> Array.filter (fun s -> s.Length > 0)
        i, nonEmpty |> List.ofArray)
    |> List.collect (fun (i, items) -> [for item in items -> { Token = stringToToken i item; Location = i } ])
