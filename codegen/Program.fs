open System.IO
open Tokenizer
open AbstractSyntaxTree

let lines =
    File.ReadAllLines @"F:\Workbench\nativeimpl\test-interface-def.txt"
    |> Array.mapi (fun i line -> (i + 1, line))
    |> Array.filter (fun (_, line) -> line.Length > 0 && not (line.StartsWith("//"))) // drop empty lines immediately once we have line numbers
    |> Array.toList

let tokens =
    tokenize lines

let tree =
    parseTokens tokens

printfn "tree: %A" tree


