open Parser
open Generators.Frontend
open Generators.Backend

let files =
    [  "/Users/daniel/Desktop/Development/nativeimpl/importme.txt"
       "/Users/daniel/Desktop/Development/nativeimpl/testing.txt" ]

let moduleMap =
    loadModule "/Users/daniel/Desktop/Development/nativeimpl/" Map.empty "Testing"
    
printfn "moduleMap: %A" moduleMap
//CppGenerator.generate moduleDef
//CsharpGenerator.generate moduleDef
//DlangGenerator.generate moduleDef
