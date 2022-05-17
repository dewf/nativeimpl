open System.IO
open Tokenizer
open Parser

let tokens =
    File.ReadAllText @"F:\Workbench\nativeimpl\test-interface-def.txt"
    |> tokenize
    
let module_ =
    parseTokens tokens
    |> resolveUserTypes

//printfn "module: %A" module_

CppGenerator.generate module_
CsharpGenerator.generate module_
