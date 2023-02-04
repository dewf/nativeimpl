module Util

open System

let lower (text: string) =
    text.ToLower()
    
let lowerFirst (text: string) =
    Char.ToLower(text[0]).ToString() + text.Substring(1)

let lowerFirstLetter (text: string) =
    Char.ToLower(text[0]).ToString()

let upperFirst (text: string) =
    Char.ToUpper(text[0]).ToString() + text.Substring(1)

let upperOnly (text: string) =
    let upper =
        text.ToCharArray()
        |> Array.filter Char.IsUpper
    String(upper)

let withTrailingCommas (things: 'a list) =
    match things |> List.rev with
    | h :: tail ->
        (h, "") :: (tail |> List.map (fun x -> x, ","))
    | _ ->
        failwithf "withCommas: things had no elements"
    |> List.rev

let tryParseInt64 (str: string) =
    match Int64.TryParse(str) with
    | true, value -> Some value
    | _ -> None
    
let unfoldState (f: 'state -> 'state option) (init: 'state): 'state =
    let rec recInner (current: 'state) =
        match f current with
        | None -> current
        | Some next -> recInner next
    recInner init
