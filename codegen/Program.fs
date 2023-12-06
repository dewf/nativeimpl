open Parser
open Generators.Frontend
open Generators.Backend

let inputs =
    [ "Nested.Woot"; "ImportMe"; "Testing" ]
let moduleMap =
    (Map.empty, inputs)
    ||> List.fold (fun acc name ->
        let m, nextAcc = loadModule "/Users/daniel/Desktop/Development/nativeimpl/testroot" acc name
        printfn "TODO: rendering module: %s" m.Name
        nextAcc)
    
printfn "moduleMap: %A" moduleMap
//CppGenerator.generate moduleDef
//CsharpGenerator.generate moduleDef
//DlangGenerator.generate moduleDef
