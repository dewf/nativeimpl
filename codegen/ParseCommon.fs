module ParseCommon

open Tokenizer
open Extensions

let (|Require|_|) (required: Token list) (input: TokenAndLocation list) =
    let front, etc =
        List.safeSplit required.Length input
    let frontTokens =
        front |> List.map (_.Token)
    if frontTokens = required then
        Some (front.Head.Location, etc)
    else
        None

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
        Some (front, h.Location, etc)
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
    let (|NameList|_|) (delimiter: Token) (maybeEndToken: Token option) (input: TokenAndLocation list) =
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
            match maybeEndToken with
            | Some endToken ->
                // verify endToken in place
                match etc with
                | Lookahead 1 ([t], loc, etc) when t = endToken ->
                    Some (names, loc, etc)
                | _ ->
                    None
                    // failwithf "NameStuff.parseList: missing required terminator @ %A" loc
            | None ->
                // all good, we parsed as much as we could already!
                Some (names, loc, etc)
        | _ ->
            None
            // failwithf "NameStuff.parseList: expected name list @ %A" loc
