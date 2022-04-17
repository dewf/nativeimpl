module Parser
open Tokenizer

module List =
//    let safeTake (n: int) (xs: 'a list) =
//        if xs.Length >= n then
//            List.take n xs
//        else
//            []
    let safeTake (count: int) (xs: 'a list) =
        // this isn't tail recursive so don't use too high a count :)
        let rec takeMore (n: int) (current: 'a list) =
            match current with
            | x :: etc when n > 0 ->
                x :: takeMore (n - 1) etc
            | _ ->
                // either n = 0, or no head element available
                []
        takeMore count xs
    let safeSplit (count: int) (xs: 'a list) =
        let rec takeMore (n: int) (current: 'a list) (revFront: 'a list) =
            match current with
            | h :: tail when n > 0 ->
                takeMore (n - 1) tail (h :: revFront)
            | _ ->
                (List.rev revFront), current
        takeMore count xs []

type NIType =
    | UnitType
    | Bool
    | Int8 | Int16 | Int32 | Int64
    | Float32 | Float64
    | StringType
    | ArrayType of NIType
    | MapType of keyType: NIType * valueType: NIType
    //
    | Unresolved of name: string * line: int
    // these have to be resolved by checking a table, otherwise unresolved
    | OpaqueHandleType of name: string
    | StructType of name: string
    | InterfaceType of name: string
    | ExceptionType of name: string          // only for 'throws'
    | DelegateType of name: string
    | TypedefType of name: string

let (|Lookahead|_|) (n: int) (input: TokenAndLocation list) = // returns ([tokens], line, etc)
    let front, etc =
        List.safeSplit n input
    if front.Length = n then
        let tokens =
            front |> List.map (fun tl -> tl.Token)
        Some (tokens, front.Head.Location, etc)
    else
        None

let rec parseType (input: TokenAndLocation list) =
    match input with
    | Lookahead 1 ([Identifier name], line, etc) -> // opaque handle, struct, interface
        Unresolved (name, line), etc
    | Lookahead 1 ([UnitToken], _, etc) ->
        UnitType, etc
    | Lookahead 1 ([BoolToken], _, etc) ->
        Bool, etc
    | Lookahead 1 ([IntToken], _, etc) ->
        Int32, etc
    | Lookahead 1 ([FloatToken], _, etc) ->
        Float32, etc
    | Lookahead 1 ([StringToken], _, etc) ->
        StringType, etc
    | Lookahead 2 ([ArrayToken; LeftBracket], line, etc1) ->
        let arrayType, etc2 =
            parseType etc1
        match etc2 with
        | Lookahead 1 ([RightBracket], _, etc3) ->
            ArrayType arrayType, etc3
        | _ ->
            failwithf "failed to parse array type at line %d" line
    | Lookahead 2 ([MapToken; LeftBracket], line, etc1) ->
        let keyType, etc2 =
            parseType etc1
        match etc2 with
        | Lookahead 1 ([Comma], line, etc3) ->
            let valueType, etc4 =
                parseType etc3
            match etc4 with
            | Lookahead 1 ([RightBracket], _, etc5) ->
                MapType (keyType, valueType), etc5
            | _ ->
                failwithf "missing ] for array type at line %d" line
        | _ ->
            failwithf "missing comma for array type at line %d" line
    | [] ->
        failwithf "parseType called on an empty list!"
    | {Location = line} :: _ ->
        failwithf "error parsing type at line %d" line

type NamedFunction = {
    Name: string
    Args: (string * NIType) list
    Returns: NIType
    Throws: NIType option // only Exceptions are allowed ...
}

type NamedField = {
    Name: string
    Type_: NIType
}

type DelegateInfo = {
    Args: (string * NIType) list
    Returns: NIType
    // no exceptions for now
}

type UserType =
    | OpaqueHandleDef
    | StructDef of fields: NamedField list
    | ExceptionDef of fields: NamedField list
    | InterfaceDef of methods: NamedFunction list
    | DelegateDef of info: DelegateInfo
    | TypeDef of actual: NIType

type ModuleDefinition =
    | Global of name: string * type_: NIType
    | FunctionDef of NamedFunction
    | UserType of name: string * type_: UserType

type Module = {
    Name: string
    Defs: ModuleDefinition list
}

let parseOneField (input: TokenAndLocation list) =
    if input.IsEmpty then
        None
    else
        let niType, etc =
            parseType input
        match etc with
        | Lookahead 2 ([Identifier name; StatementEnd], _, etc2) ->
            ({ Name = name; Type_ = niType }, etc2)
            |> Some
        | {Location = line} :: _ ->
            failwithf "missing expected identifier at line %d" line
        | [] ->
            // grab line from the start of input, which had at least 1 token
            match input with
            | {Location = line} :: _ ->
                failwithf "missing expected identifier at line %d" line
            | _ ->
                failwithf "parseOneField: shouldn't happen"

let parseFields (line: int) (input: TokenAndLocation list) =
    let closeIndex =
        input
        |> List.tryFindIndex (fun tl -> tl.Token = RightBrace)
    match closeIndex with
    | Some index ->
        let content, theRest =
            input
            |> List.splitAt index
        let items =
            List.unfold parseOneField content
        items, theRest |> List.skip 1 // don't want the brace!
    | None ->
        failwithf "parseFields @ line %d: no closing } found" line

let parseNamedFunction (input: TokenAndLocation list) =
    if input.IsEmpty then
        None
    else
        let returnType, etc =
            parseType input
        match etc with
        | Lookahead 2 ([Identifier funcName; LeftParen], line, etc2) ->
            let rightParenIndex =
                etc2 |> List.tryFindIndex (fun tl -> tl.Token = RightParen)
            match rightParenIndex with
            | Some index ->
                let argTokens, etc3 =
                    etc2 |> List.splitAt index
                let parseArg (remaining: TokenAndLocation list) =
                    if remaining.IsEmpty then
                        None
                    else
                        let type_, etc4 =
                            parseType remaining
                        match etc4 with
                        | Lookahead 2 ([Identifier name; Comma], _, etc5) ->
                            Some ((name, type_), etc5)
                        | [{ Location = _; Token = Identifier name }] -> // TODO: fix lookahead to use N as 'max', so it returns UP TO that many?
                            Some ((name, type_), [])
                        | _ -> failwithf "error parsing function arg at line %d: %A" line etc4
                let args =
                    List.unfold parseArg argTokens
                match etc3 with
                | Lookahead 2 ([RightParen; StatementEnd], _, etc4) ->
                    ({ Name = funcName; Args = args; Returns = returnType; Throws = None }, etc4)
                    |> Some
                | Lookahead 4 ([RightParen; ThrowsToken; Identifier exceptionName; StatementEnd], line, etc4) ->
                    ({ Name = funcName; Args = args; Returns = returnType; Throws = Some (Unresolved (exceptionName, line)) }, etc4)
                    |> Some
                | _ ->
                    failwithf "failed to parse function at line %d" line
            | None ->
                failwithf "function at line %d missing closing )" line
        | { Location = line } :: _ ->
            failwithf "was expecting identifier at line %d" line
        | _ ->
            // grab line from the start of input, which had at least 1 token (or it would have failed already)
            match input with
            | {Location = line} :: _ ->
                failwithf "missing expected identifier at line %d" line
            | _ ->
                failwithf "parseNamedFunction: shouldn't happen"

let parseFunctionList (line: int) (input: TokenAndLocation list) =
    let rightBraceIndex =
        input |> List.tryFindIndex (fun tl -> tl.Token = RightBrace)
    match rightBraceIndex with
    | Some index ->
        let content, etc =
            input |> List.splitAt index
        let funcList =
            List.unfold parseNamedFunction content
        match etc with
        | Lookahead 1 ([RightBrace], _, etc2) ->
            funcList, etc2
        | _ ->
            failwithf "parseFunctionList - shouldn't ever see this"
    | None ->
        failwithf "error parsing function list at line %d - missing closing }" line

let parseModule (input: TokenAndLocation list) =
    let parseNext (remaining: TokenAndLocation list) =
        match remaining with
        | Lookahead 3 ([OpaqueToken; Identifier name; StatementEnd], _, etc) ->
            (UserType (name, OpaqueHandleDef), etc)
            |> Some
        | Lookahead 3 ([StructToken; Identifier name; LeftBrace], line, etc) ->
            let fields, etc2 =
                parseFields line etc
            (UserType (name, StructDef fields), etc2)
            |> Some
        | Lookahead 3 ([ExceptionToken; Identifier name; LeftBrace], line, etc) ->
            let fields, etc2 =
                parseFields line etc
            (UserType (name, ExceptionDef fields), etc2)
            |> Some
        | Lookahead 3 ([InterfaceToken; Identifier name; LeftBrace], line, etc) ->
            let methods, etc2 =
                parseFunctionList line etc
            (UserType (name, InterfaceDef methods), etc2)
            |> Some
        | Lookahead 1 ([DelegateToken], _, etc) ->
            parseNamedFunction etc
            |> Option.map (fun (nf, etc2) ->
                (UserType (nf.Name, DelegateDef { Args = nf.Args; Returns = nf.Returns }), etc2))
        | Lookahead 1 ([TypedefToken], line, etc) ->
            let actual, etc2 =
                parseType etc
            match etc2 with
            | Lookahead 2 ([Identifier name; StatementEnd], _, etc3) ->
                (UserType (name, TypeDef actual), etc3)
                |> Some
            | _ ->
                failwithf "expected typedef name at line %d" line
        | [] ->
            // end of file!
            None
        | etc ->
            // either a global or a function, always starts with a type
            let t1, etc2 =
                parseType etc
            match etc2 with
            | Lookahead 2 ([Identifier name; StatementEnd], _, etc3) ->
                (Global (name, t1), etc3)
                |> Some
            | Lookahead 2 ([Identifier _; LeftParen], _, _) ->
                parseNamedFunction etc // note using 'etc' here (rewinding a bit because parseNamedFunction handles identifier etc too)
                |> Option.map (fun (namedFunc, etc3) -> FunctionDef namedFunc, etc3)
                // only way it returns 'None' is if its token list is empty, which we already know isn't the case
            | {Location = line; Token = token} :: _ ->
                failwithf "unexpected token %A at line %d" token line
            | [] ->
                failwith "unexpected end of file"
    List.unfold parseNext input

let parseTokens (input: TokenAndLocation list) =
    match input with
    | Lookahead 3 ([ModuleToken; Identifier name; StatementEnd], _, etc) ->
        { Name = name; Defs = parseModule etc }
    | _ ->
        failwith "createSyntaxTree: didn't find module definition"

let rec resolveType (userTypes: Map<string, UserType>) (type_: NIType) =
    match type_ with
    | ArrayType niType ->
        ArrayType (resolveType userTypes niType)
    | MapType (keyType, valueType) ->
        MapType (resolveType userTypes keyType, resolveType userTypes valueType)
    | Unresolved (name, line) ->
        match userTypes.TryFind(name) with
        | Some value ->
            match value with
            | OpaqueHandleDef -> OpaqueHandleType name
            | StructDef _ -> StructType name
            | InterfaceDef _ -> InterfaceType name
            | DelegateDef _ -> DelegateType name
            | TypeDef _ -> TypedefType name
            | ExceptionDef _ ->
                // how do we enforce that this is only allowed in a "throws" context?
                ExceptionType name
        | None ->
            failwithf "failed to find symbol '%s' at line %d" name line
    | _ ->
        type_

let resolveFieldType (userTypes: Map<string, UserType>) (ft: NamedField) =
    let nextType =
        resolveType userTypes ft.Type_
    { ft with Type_ = nextType }

let resolveFunctionTypes (userTypes: Map<string, UserType>) (nf: NamedFunction) =
    let nextArgs =
        nf.Args
        |> List.map (fun (name, argType) -> (name, resolveType userTypes argType))
    let nextReturns =
        resolveType userTypes nf.Returns
    let nextThrows =
        nf.Throws
        |> Option.map (resolveType userTypes)
    { nf with Args = nextArgs; Returns = nextReturns; Throws = nextThrows }

let resolveDelegateTypes (userTypes: Map<string, UserType>) (info: DelegateInfo) =
    let nextArgs =
        info.Args
        |> List.map (fun (name, argType) -> (name, resolveType userTypes argType))
    let nextReturns =
        resolveType userTypes info.Returns
    { info with Args = nextArgs; Returns = nextReturns }

let resolveModuleDef (userTypes: Map<string, UserType>) (def: ModuleDefinition) =
    match def with
    | UserType (name, userType) ->
        let nextUserType =
            match userType with
            | StructDef fieldTypes ->
                let nextFieldTypes =
                    fieldTypes
                    |> List.map (resolveFieldType userTypes)
                StructDef nextFieldTypes
            | ExceptionDef fieldTypes ->
                let nextFieldTypes =
                    fieldTypes
                    |> List.map (resolveFieldType userTypes)
                ExceptionDef nextFieldTypes
            | InterfaceDef namedFunctions ->
                let nextFunctions =
                    namedFunctions
                    |> List.map (resolveFunctionTypes userTypes)
                InterfaceDef nextFunctions
            | DelegateDef delegateInfo ->
                let nextInfo =
                    resolveDelegateTypes userTypes delegateInfo
                DelegateDef nextInfo
            | _ ->
                // untouched ('opaque' only for now ...)
                userType
        UserType (name, nextUserType)
    | Global (name, niType) ->
        Global (name, resolveType userTypes niType)
    | FunctionDef namedFunction ->
        FunctionDef (resolveFunctionTypes userTypes namedFunction)

let getUserTypes (module_: Module) =
    module_.Defs
    |> List.choose (function
        | UserType (name, definition) -> Some (name, definition)
        | _ -> None)
    |> Map.ofList

let resolveUserTypes (module_: Module) =
    let userTypes =
        getUserTypes module_
    // go throughout whole tree and replace all Unresolved types with things from the table, if they even exist
    let nextDefs =
        module_.Defs
        |> List.map (resolveModuleDef userTypes)
    { module_ with Defs = nextDefs }
