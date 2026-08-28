module Util

open System
open System.Text.RegularExpressions
open Extensions

let lower (text: string) =
    text.ToLower()

let lowerFirst (text: string) =
    Char.ToLower(text[0]).ToString() + text.Substring(1)

let lowerFirstWithUnderscore (text: string) =
    "_" + lowerFirst text

let lowerFirstLetter (text: string) =
    Char.ToLower(text[0]).ToString()

let underscoresRegex =
    Regex(@"_+")

type UnderscoreSegmentPolicy =
    | Ignore
    | ProcessIndividually

let upperFirst (policy: UnderscoreSegmentPolicy) (text: string) =
    let upperFunc (x: string) =
        Char.ToUpper(x[0]).ToString() + x.Substring(1)
    match policy with
    | Ignore ->
         upperFunc text
    | ProcessIndividually ->
        String.splitMap underscoresRegex upperFunc text

let upperOnly (text: string) =
    let upper =
        text.ToCharArray()
        |> Array.filter Char.IsUpper
    String(upper)

let withTrailingCommas (things: 'a list) =
    match things |> List.rev with
    | h :: tail ->
        (h, "") :: (tail |> List.map (fun x -> x, ","))
        |> List.rev
    | [] ->
        []

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

let maybeString (s: String) =
    if s.Length > 0 then
        Some s
    else
        None
