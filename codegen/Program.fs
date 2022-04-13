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

let module_ =
    parseTokens tokens
    |> resolveUserTypes

//printfn "module: %A" module_

//let file = new StreamWriter(@"F:\Workbench\nativeimpl\server\cpp_output\wrappers.cpp")
CppGenerator.generate module_ stdout

