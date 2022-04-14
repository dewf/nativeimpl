module Util

open System

let lowerFirst (text: string) =
    Char.ToLower(text[0]).ToString() + text.Substring(1)

let upperFirst (text: string) =
    Char.ToUpper(text[0]).ToString() + text.Substring(1)
