open System
open System.IO
open Generators.Backend
open Generators.Frontend
open Parser
open AppCommon

let PROJECT_PATH = @"R:\projects\gauntlet\project.toml"
let projectInfo =
    ProjectParser.parseProjectInfo PROJECT_PATH

let getDependencyOrder (moduleMap: Map<ModuleName, Module>) =
    // create "imported by" map ('name' -> modules importing 'name')
    let nameAndImports =
        moduleMap
        |> Map.toList
        |> List.map (fun (name, module_) -> (name, module_.Imports))
    let importedBy =
        (Map.empty, nameAndImports)
        ||> List.fold (fun reqMap (thisName, imports) ->
            (reqMap, imports)
            ||> List.fold (fun reqMap depName ->
                // if depName exists, prepend to its requirers
                match reqMap.TryFind depName with
                | Some importingModules ->
                    reqMap |> Map.add depName ((thisName :: importingModules) |> List.distinct)
                | None ->
                    reqMap |> Map.add depName [thisName]))
    // start from "root" files (not required by others) and recurse getting all import names
    // deepest names ought to be initialized first since others depend on them
    let roots =
        moduleMap
        |> Map.toList
        |> List.filter (fun (name, _) ->
            // start from modules that aren't used by others
            importedBy.ContainsKey name |> not)
        |> List.map fst
    let rec innerRec (m: Module) (previous: ModuleName list) =
        let nested =
            m.Imports
            |> List.collect (fun name -> innerRec moduleMap[name] [])
        nested @ m.Name :: previous
    roots
    |> List.collect (fun name -> innerRec moduleMap[name] [])
    |> List.distinct
    |> List.map (fun name -> moduleMap[name])

let appRunner (inputPath: string) (inputModules: string list) (outParams: OutParams) (frontLang: string) (backLang: string) =
    // read last run time from disk
    let lastRunTimeFilePath =
        Path.Combine(projectInfo.ProjectFileLocation, "__last_run_time.txt")

    let maybeLastRunTime =
        if File.Exists(lastRunTimeFilePath) then
            let raw = File.ReadAllText(lastRunTimeFilePath)
            DateTime.Parse(raw)
            |> Some
        else
            None
    let thisRunTime =
        // take the current time, in case the user is editing anything while we run ...
        DateTime.Now

    let moduleMap =
        (Map.empty, inputModules)
        ||> List.fold (fun moduleMap joinedName ->
            let name =
                joinedName.Split(".")
                |> Array.toList
                |> ModuleName

            printfn "======= processing module %A =======" name
            let currentModule, nextMap =
                loadModule inputPath moduleMap name Set.empty
            let context =
                { Module = currentModule; Loaded = nextMap } // nextMap! moduleMap accumulator will not yet be updated!

            // can we avoid writing this one?
            let fileNeedsWriting =
                match maybeLastRunTime with
                | Some lastRunTime ->
                    if currentModule.LastModified >= lastRunTime then
                        // file itself has been modified since last run
                        true
                    else
                        // what about imports?
                        currentModule.Imports
                        |> List.map (fun moduleName -> nextMap[moduleName]) // nextMap! moduleMap accumulator will not yet be updated!
                        |> List.exists (fun import -> import.LastModified >= lastRunTime)
                | None ->
                    true

            if fileNeedsWriting then
                // frontend
                match frontLang.ToLower() with
                | "csharp" | "c#" ->
                    printfn "rendering C# frontend code"
                    CsharpGenerator.generateModule context outParams
                | _ ->
                    failwithf "unsupported frontend language: %s" frontLang

                // backend
                match backLang.ToLower() with
                | "cpp" | "c++" ->
                    printfn "rendering C++ backend code"
                    CppGenerator.generateModule context outParams
                | _ ->
                    failwithf "unsupported backend language: %s" backLang
            else
                printfn ">> skipping module -  neither module nor its imports have been modified since last run"

            nextMap)

    printfn "======= generating library files ======="
    // get dependency order first
    let depsOrder = getDependencyOrder moduleMap

    // frontend
    match frontLang.ToLower() with
    | "csharp" | "c#" ->
        printfn "rendering C# library files"
        CsharpGenerator.generateLibraryFile depsOrder outParams
        CsharpGenerator.generateProject outParams
        CsharpGenerator.copySupportFiles outParams
    | _ ->
        failwithf "unsupported frontend language: %s" frontLang

    // backend
    match backLang.ToLower() with
    | "cpp" | "c++" ->
        printfn "rendering C++ library files"
        CppGenerator.generateLibrary depsOrder outParams
        CppGenerator.copySupportFiles outParams
        let modules =
            moduleMap
            |> Map.toList
            |> List.map snd
            |> List.sortDescending
        CppGenerator.generateProject modules outParams
    | _ ->
        failwithf "unsupported backend language: %s" backLang

    // core/DLL project (statically linked to backend)

    // printfn "moduleMap: %A" moduleMap

    // finally write current run time for next time
    // (only do this at the very end, because if we have a crash, we've written a false time, and files won't be processed!)
    File.WriteAllText(lastRunTimeFilePath, thisRunTime.ToString("O"))

let outParams =
    { Path = projectInfo.OutputPath
      MaybeNamespace = Some projectInfo.Namespace // more of a prefix, really - still will inlcude LibraryName at the end
      LibraryName = projectInfo.LibraryName }
appRunner projectInfo.InputPath projectInfo.Inputs outParams projectInfo.FrontLang projectInfo.BackLang
