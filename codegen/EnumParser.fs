module EnumParser

open Tokenizer

type EnumLiteral =
    | HexNumber of digits: string
    | DecNumber of digits: string
    | EnumIdentifier of id: string
and EnumUnaryOp =
    | BitwiseNot
and EnumBinaryOp =
    | LeftShift
    | BitwiseAnd
    | BitwiseXor
    | BitwiseOr
and EnumExpr =
    | Literal of value: EnumLiteral
    | Unary of op: EnumUnaryOp * value: EnumExpr
    | Binary of op: EnumBinaryOp * left: EnumExpr * right: EnumExpr
    | Grouping of inner: EnumExpr
    
let private (|MatchLiteral|_|) (input: TokenAndLocation list) =
    match input with
    | { Token = token } :: etc ->
        match token with
        | Identifier text -> Some (EnumIdentifier text, etc)
        | Digits digits -> Some (DecNumber digits, etc)
        | HexDigits digits -> Some (HexNumber digits, etc)
        | _ -> None
    | _ ->
        None
        
let private (|MatchUnaryOp|_|) (input: TokenAndLocation list) =
    match input with
    | { Token = token } :: etc ->
        match token with
        | Tilde -> Some (BitwiseNot, etc)
        | _ -> None
    | _ ->
        None
        
let private (|MatchBinaryOp|_|) (input: TokenAndLocation list) =
    match input with
    | { Token = token } ::  etc ->
        match token with
        | LeftShiftToken -> Some (LeftShift, etc)
        | PipeToken -> Some (BitwiseOr, etc)
        | Ampersand -> Some (BitwiseAnd, etc)
        | Caret -> Some (BitwiseXor, etc)
        | _ -> None
    | _ ->
        None
        
let private (|MatchGrouping|_|) (input: TokenAndLocation list) =
    match input with
    | { Token = LeftParen; Location = loc } :: etc ->
        // scan to matching right paren
        let innerTokens =
            {| Level = 1; Remaining = etc |}
            |> List.unfold (fun state ->
                match state.Remaining with
                | { Token = LeftParen } as h :: etc ->
                    Some (h, {| state with Level = state.Level + 1; Remaining = etc |})
                | { Token = RightParen } as h :: etc ->
                    if state.Level - 1 > 0 then
                        // more to go
                        Some (h, {| state with Level = state.Level - 1; Remaining = etc |})
                    else
                        // done!
                        None
                | h :: etc ->
                    // anything else, pass through as-is
                    Some (h, {| state with Remaining = etc |})
                | [] ->
                    // ran out of tokens before completing?
                    failwithf "un-closed group starting at %A" loc)
        let _, etc =
            List.splitAt (innerTokens.Length + 1) etc // +1 to account for right paren
        Some (innerTokens, etc)
    | _ ->
        None

[<RequireQualifiedAccess>]
type private StackOp =
    | Unary of op: EnumUnaryOp
    | Binary of op: EnumBinaryOp
    | Sentinel
    
let private precedence = function
    | StackOp.Unary op ->
        match op with
        | BitwiseNot -> 10
    | StackOp.Binary op ->
        match op with
        | LeftShift -> 9
        | BitwiseAnd -> 8
        | BitwiseXor -> 7
        | BitwiseOr -> 6
    | StackOp.Sentinel ->
        // lowest by definition, forces evaluation of all ops
        0

let private pushStackOp (operands: EnumExpr list) (opStack: StackOp list) (toPush: StackOp) =
    // while precedence opStack.top > precedence op do ...
    let rec removeWhileHigher (operands: EnumExpr list) (opStack: StackOp list) =
        match opStack with
        | top :: opStackEtc ->
            if precedence top > precedence toPush then
                // apply it
                let nextOperands =
                    match top with
                    | StackOp.Unary op ->
                        match operands with
                        | right :: operandsEtc ->
                            Unary (op, right) :: operandsEtc
                        | [] ->
                            failwithf "pushStackOp:removeOne - no operands available for unary op %A" op
                    | StackOp.Binary op ->
                        match operands with
                        | right :: left :: operandsEtc ->
                            Binary (op, left, right) :: operandsEtc
                        | _ ->
                            failwithf "pushStackOp:removeOne - not enough operands available for binary op %A" op
                    | StackOp.Sentinel ->
                        failwith "pushStackOp:removeOne - should never occur (sentinel not greater precedence than any)"
                // keep going ...
                removeWhileHigher nextOperands opStackEtc
            else
                // top is not greater precedence, leave alone
                operands, opStack
        | [] ->
            // no operations, return as-is
            operands, opStack
    let nextOperands, nextOpStack =
        removeWhileHigher operands opStack
    // finally, push
    nextOperands, toPush :: nextOpStack

let rec private parseEnumValue (operands: EnumExpr list) (opStack: StackOp list) (input: TokenAndLocation list) =
    match input with
    | MatchLiteral (literal, etc) ->
        let nextOperands =
            Literal literal :: operands
        parseEnumValue nextOperands opStack etc
    | MatchUnaryOp (op, etc) ->
        let nextOperands, nextOpStack =
            pushStackOp operands opStack (StackOp.Unary op)
        parseEnumValue nextOperands nextOpStack etc
    | MatchGrouping (contents, etc) ->
        let nextOperands =
            let expr =
                parseEnumValue [] [] contents
            (Grouping expr) :: operands
        parseEnumValue nextOperands opStack etc
    | MatchBinaryOp (op, etc) ->
        let nextOperands, nextOpStack =
            pushStackOp operands opStack (StackOp.Binary op)
        parseEnumValue nextOperands nextOpStack etc
    | { Token = token; Location = loc } :: _ ->
        failwithf "parse2: unexpected token [%A] @ %A when parsing enum expression" token loc
    | [] ->
        // reached the end, just collapse whatever's there!
        let nextOperands, _ =
            pushStackOp operands opStack StackOp.Sentinel // lowest precedence, will force the evaluation of all other operators
        match nextOperands with
        | [ result ] -> result
        | _ :: _ -> failwith "parse2: too many operands for final result!"
        | [] -> failwith "parse2: no operands available for final result!"
        
let parseEnumExpression (input: TokenAndLocation list) =
    parseEnumValue [] [] input
                    
let rec exprString (expr: EnumExpr) =
    match expr with
    | Literal value ->
        match value with
        | HexNumber digits -> digits
        | DecNumber digits -> digits
        | EnumIdentifier id -> id
    | Unary(op, value) ->
        let opStr =
            match op with
            | BitwiseNot -> "~"
        sprintf "%s%s" opStr (exprString value)
    | Binary(op, left, right) ->
        let opStr =
            match op with
            | LeftShift -> "<<"
            | BitwiseAnd -> "&"
            | BitwiseXor -> "^"
            | BitwiseOr -> "|"
        sprintf "%s %s %s" (exprString left) opStr (exprString right)
    | Grouping inner ->
        sprintf "(%s)" (exprString inner)

// let rec prettyPrintExpr (level: int) (expr: EnumExpr) =
//     let indent count =
//         List.replicate count "  "
//         |> String.concat ""
//     match expr with
//     | Literal value ->
//         printfn "%sliteral: %A" (indent level) value
//     | Unary(op, value) ->
//         printfn "%s%A:" (indent level) op
//         prettyPrintExpr (level + 1) value
//     | Binary(op, left, right) ->
//         printfn "%s%A:" (indent level) op
//         printfn "%sleft:" (level + 1 |> indent)
//         prettyPrintExpr (level + 2) left
//         printfn "%sright:" (level + 1 |> indent)
//         prettyPrintExpr (level + 2) right
//     | Grouping inner ->
//         printfn "%sgroup(" (indent level)
//         prettyPrintExpr (level + 1) inner
//         printfn "%s)end" (indent level)
//
// let result = parseEnumValue [] [] (tokenize "3 | ~5 & 2 | (Whatever << ~6 | 0x0001) | 2")
// printfn "flat: [%s]" (exprString result)
