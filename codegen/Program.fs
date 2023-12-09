open Parser
open Generators.Frontend
open Generators.Backend

let INPUT_PATH = "/Users/daniel/Desktop/Development/nativeimpl/testroot"
let OUTPUT_PATH = "/Users/daniel/Desktop/ni-output"
let INPUTS = [ "Nested.Woot"; "ImportMe"; "Testing" ]
let NAMESPACE = "TestLibrary"
let FRONT_LANG = "csharp"
let BACK_LANG = "cpp"

let appRunner (inputPath: string) (inputModules: string list) (outPath: string) (outNamespace: string) (frontLang: string) (backLang: string) =
    let moduleMap =
        (Map.empty, inputModules)
        ||> List.fold (fun moduleMap name ->
            let currentModule, nextMap = loadModule inputPath moduleMap name
            // frontend
            match frontLang.ToLower() with
            | "csharp" | "c#" ->
                printfn "rendering C# frontend code"
                CsharpGenerator.generateModule currentModule outPath (outNamespace + "Client")
            | _ ->
                failwithf "unsupported frontend language: %s" frontLang
            // backend
            match backLang.ToLower() with
            | "cpp" | "c++" ->
                printfn "rendering C++ backend code"
                // CppGenerator.generate m
            | _ ->
                failwithf "unsupported backend language: %s" backLang
            nextMap)
    // printfn "moduleMap: %A" moduleMap
    // generate project files:
    CsharpGenerator.generateProject outPath outNamespace
    // generate project files for backend (eg cmake)

appRunner INPUT_PATH INPUTS OUTPUT_PATH NAMESPACE FRONT_LANG BACK_LANG

//CppGenerator.generate moduleDef
//CsharpGenerator.generate moduleDef
//DlangGenerator.generate moduleDef
