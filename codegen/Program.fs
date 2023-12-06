open Parser
open Generators.Frontend
open Generators.Backend

let files =
    [  "/Users/daniel/Desktop/Development/nativeimpl/importme.txt"
       "/Users/daniel/Desktop/Development/nativeimpl/testing.txt" ]

let moduleDef =
    loadModule "/Users/daniel/Desktop/Development/nativeimpl/" Map.empty "Testing"
    
printfn "module: %A" moduleDef
//CppGenerator.generate moduleDef
//CsharpGenerator.generate moduleDef
//DlangGenerator.generate moduleDef
