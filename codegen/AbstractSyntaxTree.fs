module AbstractSyntaxTree

open Tokenizer

type NIType =
    | UnitType
    | Bool
    | Int8 | Int16 | Int32 | Int64
    | Float32 | Float64
    | StringType
    | ArrayType of NIType
    | MapType of keyType: NIType * valueType: NIType
    | AnonymousFunction of args: NIType list * returns: NIType
    // these have to be resolved by checking a table, otherise unresolved
    | OpaqueHandleType of name: string
    | StructType of name: string
    | InterfaceType of name: string
    | Unresolved of name: string * line: int // something that hasn't been declared yet?

let rec parseType (tokens: (int * Token) list) =
    match tokens with
    | (line, Identifier name) :: etc -> // opaque handle, struct, interface
        Unresolved (name, line), etc
    | (_, UnitToken) :: etc ->
        UnitType, etc
    | (_, BoolToken) :: etc ->
        Bool, etc
    | (_, IntToken) :: etc ->
        Int32, etc
    | (_, FloatToken) :: etc ->
        Float32, etc
    | (_, StringToken) :: etc ->
        StringType, etc
    | (line, ArrayToken) :: (_, LeftBracket) :: etc1 ->
        let arrayType, etc2 =
            parseType etc1
        match etc2 with
        | (_, RightBracket) :: etc3 ->
            ArrayType arrayType, etc3
        | _ ->
            failwithf "failed to parse array type at line %d" line
    | (line, MapToken) :: (_, LeftBracket) :: etc1 ->
        let keyType, etc2 =
            parseType etc1
        match etc2 with
        | (_, Comma) :: etc3 ->
            let valueType, etc4 =
                parseType etc3
            match etc4 with
            | (_, RightBracket) :: etc5 ->
                MapType (keyType, valueType), etc5
            | _ ->
                failwithf "missing ] for array type at line %d" line
        | _ ->
            failwithf "missing comma for array type at line %d" line
    | (line, FunctionBegin) :: etc1 ->
        // keep parsing types (separated by commas) until we see a function arrow ->, then grab a return type
        // find the function arrow first, if any
        let maybeArrowIndex =
            etc1
            |> List.tryFindIndex (fun (_, t) -> t = FunctionArrow)
        match maybeArrowIndex with
        | Some index ->
            let argTokens, etc2 =
                etc1 |> List.splitAt index
            let parseArg (remaining: (int * Token) list) =
                if remaining.IsEmpty then
                    None
                else
                    let type_, etc3 =
                        parseType remaining
                    match etc3 with
                    | (_, Comma) :: etc4 -> Some (type_, etc4)
                    | [] -> Some (type_, [])
                    | _ -> failwithf "error parsing function type at line %d" line
            let args =
                List.unfold parseArg argTokens
            match etc2 with
            | (_, FunctionArrow) :: etc4 ->
                let returnType, etc5 =
                    parseType etc4
                AnonymousFunction (args, returnType), etc5
            | _ ->
                failwithf "failed to parse function return type at line %d" line
        | None ->
            failwithf "missing function arrow -> at line %d" line
    | [] ->
        failwithf "parseType called on an empty list!"
    | _ ->
        let lineStr =
            tokens |> List.tryHead |> Option.map (fun (li, _) -> sprintf "%d" li) |> Option.defaultValue "???"
        failwithf "error parsing type at line %s" lineStr

type NamedFunction = {
    Name: string
    Args: (string * NIType) list
    Returns: NIType
    Throws: string option   // name of exception
}

type FieldType = {
    Name: string
    Type_: NIType
}

type ModuleDefinition =
    | OpaqueHandleDef of name: string
    | Global of name: string * type_: NIType
    | StructDef of name: string * fields: FieldType list
    | ExceptionDef of name: string * fields: FieldType list
    | InterfaceDef of name: string * methods: NamedFunction list
    | FunctionDef of NamedFunction

type Module =
    | Module of name: string * defs: ModuleDefinition list

module List =
    let safeTake (n: int) (xs: 'a list) =
        if xs.Length >= n then
            List.take n xs
        else
            []

let parseOneField (remaining: (int * Token) list) =
    if remaining.IsEmpty then
        None
    else
        let niType, etc =
            parseType remaining
        match etc with
        | (_, Identifier name) :: (_, StatementEnd) :: etc2 ->
            ({ Name = name; Type_ = niType }, etc2)
            |> Some
        | _ ->
            let lineStr =
                etc |> List.tryHead |> Option.map (fun (li, _) -> sprintf "%d" li) |> Option.defaultValue "???"
            failwithf "failed to parse field at line %s" lineStr

let parseFields (line: int) (tokens: (int * Token) list) =
    let closeIndex =
        tokens
        |> List.tryFindIndex (fun (_, t) -> t = RightBrace)
    match closeIndex with
    | Some index ->
        let content, theRest =
            tokens
            |> List.splitAt index
        let items =
            List.unfold parseOneField content
        items, theRest |> List.skip 1 // don't want the brace!
    | None ->
        failwithf "parseFields @ line %d: no closing } found" line

let parseNamedFunction (tokens: (int * Token) list) =
    if tokens.IsEmpty then
        None
    else
        let returnType, etc =
            parseType tokens
        match etc with
        | (line, Identifier funcName) :: (_, LeftParen) :: etc2 ->
            let rightParenIndex =
                etc2 |> List.tryFindIndex (fun (_, t) -> t = RightParen)
            match rightParenIndex with
            | Some index ->
                let argTokens, etc3 =
                    etc2 |> List.splitAt index
                let parseArg (remaining: (int * Token) list) =
                    if remaining.IsEmpty then
                        None
                    else
                        let type_, etc4 =
                            parseType remaining
                        match etc4 with
                        | (_, Identifier name) :: (_, Comma) :: etc5 -> Some ((name, type_), etc5)
                        | [ (_, Identifier name) ] -> Some ((name, type_), [])
                        | _ -> failwithf "error parsing function arg at line %d: %A" line etc4
                let args =
                    List.unfold parseArg argTokens
                match etc3 with
                | (_, RightParen) :: (_, StatementEnd) :: etc4 ->
                    ({ Name = funcName; Args = args; Returns = returnType; Throws = None }, etc4)
                    |> Some
                | (_, RightParen) :: (_, ThrowsToken) :: (_, Identifier exceptionName) :: (_, StatementEnd) :: etc4 ->
                    ({ Name = funcName; Args = args; Returns = returnType; Throws = Some exceptionName }, etc4)
                    |> Some
                | _ ->
                    failwithf "failed to parse function at line %d" line
            | None ->
                failwithf "function at line %d missing closing )" line
        | _ ->
            let lineStr =
                tokens |> List.tryHead |> Option.map (fun (li, _) -> sprintf "%d" li ) |> Option.defaultValue "???"
            failwithf "was expecting identifier at line %s" lineStr

let parseFunctionList (line: int) (tokens: (int * Token) list) =
    let rightBraceIndex =
        tokens |> List.tryFindIndex (fun (_, t) -> t = RightBrace)
    match rightBraceIndex with
    | Some index ->
        let content, etc =
            tokens |> List.splitAt index
        let funcList =
            List.unfold parseNamedFunction content
        match etc with
        | (_, RightBrace) :: etc2 ->
            funcList, etc2
        | _ ->
            failwithf "parseFunctionList - shouldn't ever see this"
    | None ->
        failwithf "error parsing function list at line %d - missing closing }" line

let parseModule (tokens: (int * Token) list) =
    let parseNext (remaining: (int * Token) list) =
        match remaining with
        | (_, OpaqueToken) :: (_, Identifier name) :: (_, StatementEnd) :: etc ->
            (OpaqueHandleDef name, etc)
            |> Some
        | (line, StructToken) :: (_, Identifier name) :: (_, LeftBrace) :: etc ->
            let fields, etc2 =
                parseFields line etc
            (StructDef (name, fields), etc2)
            |> Some
        | (line, ExceptionToken) :: (_, Identifier name) :: (_, LeftBrace) :: etc ->
            let fields, etc2 =
                parseFields line etc
            (ExceptionDef (name, fields), etc2)
            |> Some
        | (line, InterfaceToken) :: (_, Identifier name) :: (_, LeftBrace) :: etc ->
            let methods, etc2 =
                parseFunctionList line etc
            (InterfaceDef (name, methods), etc2)
            |> Some
        | [] ->
            // end of file!
            None
        | etc ->
            // either a global or a function, always starts with a type
            let t1, etc2 =
                parseType etc
            match etc2 with
            | (_, Identifier name) :: (_, StatementEnd) :: etc3 ->
                (Global (name, t1), etc3)
                |> Some
            | (_, Identifier _) :: (_, LeftParen) :: _ ->
                parseNamedFunction etc // note using 'etc' here (rewinding a bit because parseNamedFunction handles identifier etc too)
                |> Option.map (fun (namedFunc, etc3) -> FunctionDef namedFunc, etc3)
                // only way it returns 'None' is if its token list is empty, which we already know isn't the case
            | (line, token) :: _ ->
                failwithf "unexpected token %A at line %d" token line
            | [] ->
                failwith "unexpected end of file"
    List.unfold parseNext tokens

let parseTokens (tokens: (int * Token) list) =
    match tokens with
    | (_, ModuleToken) :: (_, Identifier name) :: (_, StatementEnd) :: theRest ->
        Module (name, parseModule theRest)
    | _ ->
        failwith "createSyntaxTree: didn't find module definition"
