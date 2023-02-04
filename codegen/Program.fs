open System.IO
open Tokenizer
open Parser

let tokens =
    File.ReadAllText @"F:\Workbench\nativeimpl\test-interface-def.txt"
    |> tokenize
    
let moduleDef =
    parseTokens tokens
    |> resolveUserTypes

//printfn "module: %A" module_

CppGenerator.generate moduleDef
DlangGenerator.generate moduleDef
//CsharpGenerator.generate moduleDef
