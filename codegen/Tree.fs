module AbstractSyntaxTree
open Tokenizer

module List =
    let safeTake (n: int) (xs: 'a list) =
        if xs.Length >= n then
            List.take n xs
        else
            []

type NIType =
    | UnitType
    | Bool
    | Int8 | Int16 | Int32 | Int64
    | Float32 | Float64
    | StringType
    | ArrayType of NIType
    | MapType of keyType: NIType * valueType: NIType
    | AnonymousFunction of args: NIType list * returns: NIType
    //
    | Unresolved of name: string * line: int
    // these have to be resolved by checking a table, otherwise unresolved
    | OpaqueHandleType of name: string
    | StructType of name: string
    | InterfaceType of name: string
    | ExceptionType of name: string          // only for 'throws'

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
    Throws: NIType option // only Exceptions are allowed ...
}

type FieldType = {
    Name: string
    Type_: NIType
}

type UserType =
    | OpaqueHandleDef
    | StructDef of fields: FieldType list
    | ExceptionDef of fields: FieldType list
    | InterfaceDef of methods: NamedFunction list

type ModuleDefinition =
    | Global of name: string * type_: NIType
    | FunctionDef of NamedFunction
    | UserType of name: string * type_: UserType

type Module = {
    Name: string
    Defs: ModuleDefinition list
}

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
                | (_, RightParen) :: (_, ThrowsToken) :: (idLine, Identifier exceptionName) :: (_, StatementEnd) :: etc4 ->
                    ({ Name = funcName; Args = args; Returns = returnType; Throws = Some (Unresolved (exceptionName, idLine)) }, etc4)
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
            (UserType (name, OpaqueHandleDef), etc)
            |> Some
        | (line, StructToken) :: (_, Identifier name) :: (_, LeftBrace) :: etc ->
            let fields, etc2 =
                parseFields line etc
            (UserType (name, StructDef fields), etc2)
            |> Some
        | (line, ExceptionToken) :: (_, Identifier name) :: (_, LeftBrace) :: etc ->
            let fields, etc2 =
                parseFields line etc
            (UserType (name, ExceptionDef fields), etc2)
            |> Some
        | (line, InterfaceToken) :: (_, Identifier name) :: (_, LeftBrace) :: etc ->
            let methods, etc2 =
                parseFunctionList line etc
            (UserType (name, InterfaceDef methods), etc2)
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
        { Name = name; Defs = parseModule theRest }
    | _ ->
        failwith "createSyntaxTree: didn't find module definition"

let rec resolveType (symbols: Map<string, UserType>) (type_: NIType) =
    match type_ with
    | ArrayType niType ->
        ArrayType (resolveType symbols niType)
    | MapType (keyType, valueType) ->
        MapType (resolveType symbols keyType, resolveType symbols valueType)
    | AnonymousFunction (args, returns) ->
        let nextArgs =
            args |> List.map (resolveType symbols)
        let nextReturns =
            resolveType symbols returns
        AnonymousFunction (nextArgs, nextReturns)
    | Unresolved (name, line) ->
        match symbols.TryFind(name) with
        | Some value ->
            match value with
            | OpaqueHandleDef -> OpaqueHandleType name
            | StructDef _ -> StructType name
            | InterfaceDef _ -> InterfaceType name
            | ExceptionDef _ ->
                // how do we enforce that this is only allowed in a "throws" context?
                ExceptionType name
        | None ->
            failwithf "failed to find symbol '%s' at line %d" name line
    | _ ->
        type_

let resolveFieldType (symbols: Map<string, UserType>) (ft: FieldType) =
    let nextType =
        resolveType symbols ft.Type_
    { ft with Type_ = nextType }

let resolveFunctionTypes (symbols: Map<string, UserType>) (nf: NamedFunction) =
    let nextArgs =
        nf.Args
        |> List.map (fun (name, argType) -> (name, resolveType symbols argType))
    let nextReturns =
        resolveType symbols nf.Returns
    let nextThrows =
        nf.Throws
        |> Option.map (resolveType symbols)
    { nf with Args = nextArgs; Returns = nextReturns; Throws = nextThrows }

let resolveModuleDef (symbols: Map<string, UserType>) (def: ModuleDefinition) =
    match def with
    | UserType (name, userType) ->
        let nextUserType =
            match userType with
            | StructDef fieldTypes ->
                let nextFieldTypes =
                    fieldTypes
                    |> List.map (resolveFieldType symbols)
                StructDef nextFieldTypes
            | ExceptionDef fieldTypes ->
                let nextFieldTypes =
                    fieldTypes
                    |> List.map (resolveFieldType symbols)
                ExceptionDef nextFieldTypes
            | InterfaceDef namedFunctions ->
                let nextFunctions =
                    namedFunctions
                    |> List.map (resolveFunctionTypes symbols)
                InterfaceDef nextFunctions
            | _ ->
                // untouched ('opaque' only for now ...)
                userType
        UserType (name, nextUserType)
    | Global (name, niType) ->
        Global (name, resolveType symbols niType)
    | FunctionDef namedFunction ->
        FunctionDef (resolveFunctionTypes symbols namedFunction)

let resolveUserTypes (module_: Module) =
    let symbols =
        module_.Defs
        |> List.choose (function
            | UserType (name, definition) -> Some (name, definition)
            | _ -> None)
        |> Map.ofList
    // go throughout whole tree and replace all Unresolved types with things from the table, if they even exist
    let nextDefs =
        module_.Defs
        |> List.map (resolveModuleDef symbols)
    { module_ with Defs = nextDefs }
