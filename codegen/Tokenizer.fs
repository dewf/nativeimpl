module Tokenizer

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
    | "throws" -> ThrowsToken
    | text when Regex(@"[_a-zA-Z][_a-zA-Z0-9]*").IsMatch(text) -> Identifier text
    | text when Regex(@"[0-9]+").IsMatch(text) -> Digits text
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
        let spaceAdded = Regex(@"[,\(\)\[\];{}<>:=]").Replace(spacesCollapsed, MatchEvaluator(fun m -> sprintf " %s " m.Value))
        let items = spaceAdded.Split(' ')
        let nonEmpty =
            items
            |> Array.filter (fun s -> s.Length > 0)
        i, nonEmpty |> List.ofArray)
    |> List.collect (fun (i, items) -> [for item in items -> { Token = stringToToken i item; Location = i } ])
