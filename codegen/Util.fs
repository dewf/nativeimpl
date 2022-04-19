module Util

open System

let lowerFirst (text: string) =
    Char.ToLower(text[0]).ToString() + text.Substring(1)

let upperFirst (text: string) =
    Char.ToUpper(text[0]).ToString() + text.Substring(1)

let withTrailingCommas (things: 'a list) =
    match things |> List.rev with
    | h :: tail ->
        (h, "") :: (tail |> List.map (fun x -> x, ","))
    | _ ->
        failwithf "withCommas: things had no elements"
    |> List.rev
