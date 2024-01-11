open Parser
open Generators.Frontend
open Generators.Backend
open Generators.Common

let INPUT_PATH = @"C:\Users\Daniel\Desktop\Development\guibase"
let OUTPUT_PATH = @"C:\Users\Daniel\Desktop\Development\guibase\output"
let INPUTS = [ "Windowing"; ]
let NAMESPACE = "Org.Prefixed"
let LIBRARY = "GuiBase"
let FRONT_LANG = "csharp"
let BACK_LANG = "cpp"

let appRunner (inputPath: string) (inputModules: string list) (outParams: OutParams) (frontLang: string) (backLang: string) =
    let moduleMap =
        (Map.empty, inputModules)
        ||> List.fold (fun moduleMap name ->
            let currentModule, nextMap = loadModule inputPath moduleMap name
            // frontend
            match frontLang.ToLower() with
            | "csharp" | "c#" ->
                printfn "rendering C# frontend code"
                CsharpGenerator.generateModule currentModule outParams
            | _ ->
                failwithf "unsupported frontend language: %s" frontLang
            // backend
            match backLang.ToLower() with
            | "cpp" | "c++" ->
                printfn "rendering C++ backend code"
                CppGenerator.generateModule outParams currentModule
            | _ ->
                failwithf "unsupported backend language: %s" backLang
            nextMap)
    // printfn "moduleMap: %A" moduleMap
    // generate project files:
    CsharpGenerator.generateProject outParams
    // generate project files for backend (eg cmake)

let outParams =
    { Path = OUTPUT_PATH; MaybeNamespace = Some NAMESPACE; LibraryName = LIBRARY }
appRunner INPUT_PATH INPUTS outParams FRONT_LANG BACK_LANG
