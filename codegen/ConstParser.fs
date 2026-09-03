module ConstParser

open Tokenizer
open ParseCommon

type ConstLiteral =
    | HexNumber of digits: string
    | DecNumber of digits: string
    | StringLiteral of str: string
and ConstUnaryOp =
    | BitwiseNot
    | Negation
and ConstBinaryOp =
    | LeftShift
    | BitwiseAnd
    | BitwiseXor
    | BitwiseOr
    | PlusOp
and UntypedConstExpr =
    | NullValue
    | BoolValue of value: bool
    | Name of parts: string list
    | Literal of value: ConstLiteral
    | Unary of op: ConstUnaryOp * value: UntypedConstExpr
    | Binary of op: ConstBinaryOp * left: UntypedConstExpr * right: UntypedConstExpr
    | Grouping of groupContent: UntypedConstExpr
    // ugly because we can't currently parse NITypes yet
    // in the future I guess we'll have to move that before/above this code
    | Cast of targetType: TokenAndLocation list * castContent: UntypedConstExpr

let private (|MatchName|_|) (input: TokenAndLocation list) =
    match input with
    | NameStuff.NameParts (parts, _, etc) ->
        Some (parts, etc)
    | _ ->
        None

let private (|MatchLiteral|_|) (input: TokenAndLocation list) =
    match input with
    | { Token = token } :: etc ->
        match token with
        // | Identifier text -> Some (EnumIdentifier text, etc)
        | Digits digits -> Some (DecNumber digits, etc)
        | HexDigits digits -> Some (HexNumber digits, etc)
        | StringLiteralToken str -> Some (StringLiteral str, etc)
        | _ -> None
    | _ ->
        None

let private (|MatchUnaryOp|_|) (input: TokenAndLocation list) =
    match input with
    | { Token = token } :: etc ->
        match token with
        | Tilde -> Some (BitwiseNot, etc)
        | Minus -> Some (Negation, etc)
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
        | Plus -> Some (PlusOp, etc)
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

let private (|MatchCast|_|) (input: TokenAndLocation list) =
    match input with
    | Lookahead 2 ([ CastToken; LeftAngleBracket ], loc, etc) ->
        let typeTokens, etc =
            contentUntilMatching loc LeftAngleBracket etc
        match etc with
        | Lookahead 1 ([ LeftParen ], loc, etc) ->
            let contentTokens, etc =
                contentUntilMatching loc LeftParen etc
            Some (typeTokens, contentTokens, etc)
        | _ ->
            failwithf "parsing cast @ %A: expected parens with content [eg: cast<int>(...)] " loc
    | _ ->
        None

[<RequireQualifiedAccess>]
type private StackOp =
    | Unary of op: ConstUnaryOp
    | Binary of op: ConstBinaryOp
    | Sentinel

let private precedence = function
    | StackOp.Unary op ->
        match op with
        | BitwiseNot -> 12
        | Negation -> 11
    | StackOp.Binary op ->
        match op with
        | PlusOp -> 10
        | LeftShift -> 9
        | BitwiseAnd -> 8
        | BitwiseXor -> 7
        | BitwiseOr -> 6
    | StackOp.Sentinel ->
        // lowest by definition, forces evaluation of all ops
        0

let private pushStackOp (operands: UntypedConstExpr list) (opStack: StackOp list) (toPush: StackOp) =
    // while precedence opStack.top > precedence op do ...
    let rec removeWhileHigher (operands: UntypedConstExpr list) (opStack: StackOp list) =
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

let rec private parseConstValue (operands: UntypedConstExpr list) (opStack: StackOp list) (input: TokenAndLocation list) =
    match input with
    | { Token = NullToken } :: _ ->
        // null can't be part of any expression, stands alone
        NullValue
    | { Token = BoolValueToken value } :: _ ->
        // bool can't (currently) be part of any expression, stands alone
        BoolValue value
    | MatchCast (targetTypeTokens, contents, etc) ->
        let nextOperands =
            let expr =
                parseConstValue [] [] contents
            (Cast (targetTypeTokens, expr)) :: operands
        parseConstValue nextOperands opStack etc
    | MatchName (parts, etc) ->
        let nextOperands =
            Name parts :: operands
        parseConstValue nextOperands opStack etc
    | MatchLiteral (literal, etc) ->
        let nextOperands =
            Literal literal :: operands
        parseConstValue nextOperands opStack etc
    | MatchUnaryOp (op, etc) ->
        let nextOperands, nextOpStack =
            pushStackOp operands opStack (StackOp.Unary op)
        parseConstValue nextOperands nextOpStack etc
    | MatchGrouping (contents, etc) ->
        let nextOperands =
            let expr =
                parseConstValue [] [] contents
            (Grouping expr) :: operands
        parseConstValue nextOperands opStack etc
    | MatchBinaryOp (op, etc) ->
        let nextOperands, nextOpStack =
            pushStackOp operands opStack (StackOp.Binary op)
        parseConstValue nextOperands nextOpStack etc
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

let parseConstExpression (input: TokenAndLocation list) =
    parseConstValue [] [] input

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
