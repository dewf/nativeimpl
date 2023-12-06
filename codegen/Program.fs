open Parser
open Generators.Frontend
open Generators.Backend

let inputs =
    [ "ImportMe"; "Testing" ]
let moduleMap =
    (Map.empty, inputs)
    ||> List.fold (loadModule "/Users/daniel/Desktop/Development/nativeimpl/")
    
printfn "moduleMap: %A" moduleMap
//CppGenerator.generate moduleDef
//CsharpGenerator.generate moduleDef
//DlangGenerator.generate moduleDef
