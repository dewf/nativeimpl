open System
open System.IO
open Argu
open Generators.Backend
open Generators.Frontend
open Parser
open AppCommon
open ProjectParser

type AppArguments =
    | [<MainCommand; ExactlyOnce; Last>] Project of path: string
    | Force_Write
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Project _ -> "specify the .toml project file"
            | Force_Write -> "always generate code even if module definitions have not changed"

type AppConfig = {
    ProjectPath: string
    AlwaysWrite: bool
} with
    static member Empty = {
        ProjectPath = ""     // a required command line argument, so not optional
        AlwaysWrite = false
    }

let appConfig =
    let parser =
        ArgumentParser.Create<AppArguments>(programName = "nativeimpl")
    let results =
        try
            parser.ParseCommandLine(raiseOnUsage=true)
        with
        | :? ArguParseException as ex ->
            eprintfn "%s" ex.Message
            exit 1
    (AppConfig.Empty, results.GetAllResults())
    ||> List.fold (fun acc arg ->
        match arg with
        | Project path ->
            { acc with ProjectPath = path }
        | Force_Write ->
            { acc with AlwaysWrite = true }
        )

let projectInfo =
    parseProjectInfo appConfig.ProjectPath

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

let appRunner (inputPath: string) (inputModules: string list) (outParams: OutParams) (frontLangs: Language list) (backLang: Language) =
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

            // TODO: validation step for module?
            // for example, would be nice to check certain things, like making sure initModule() is void(void)

            let context =
                { Module = currentModule; Loaded = nextMap } // nextMap! moduleMap accumulator will not yet be updated!

            // can we avoid writing this one?
            let fileNeedsWriting =
                if appConfig.AlwaysWrite then
                    true
                else
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
                // frontend(s)
                for frontLang in frontLangs do
                    match frontLang with
                    | CSharp ->
                        printfn "rendering C# frontend code"
                        CsharpGenerator.generateModule context outParams
                    | Dlang ->
                        printfn "rendering Dlang frontend code"
                        DlangGenerator.generateModule context outParams
                    | _ ->
                        failwithf "unsupported frontend language: %A" frontLang

                // backend
                match backLang with
                | Cpp ->
                    printfn "rendering C++ backend code"
                    CppGenerator.generateModule context outParams
                | _ ->
                    failwithf "unsupported backend language: %A" backLang
            else
                printfn ">> skipping module -  neither module nor its imports have been modified since last run"

            nextMap)

    printfn "======= generating library files ======="
    // get dependency order first
    let depsOrder = getDependencyOrder moduleMap

    printfn "====== DEPS ORDER ==========="
    for m in depsOrder do
        printfn " - %A" m.Name
    printfn "============================="

    // frontend
    for frontLang in frontLangs do
        match frontLang with
        | CSharp ->
            printfn "rendering C# library files"
            CsharpGenerator.generateLibraryFile depsOrder outParams
            CsharpGenerator.generateProject outParams
            CsharpGenerator.copySupportFiles outParams
        | Dlang ->
            printfn "rendering Dlang library files"
            DlangGenerator.generateLibraryFile depsOrder outParams
            DlangGenerator.generateProject outParams
            DlangGenerator.copySupportFiles outParams
        | _ ->
            failwithf "unsupported frontend language: %A" frontLang

    // backend
    match backLang with
    | Cpp ->
        printfn "rendering C++ library files"
        CppGenerator.generateLibrary depsOrder outParams
        CppGenerator.copySupportFiles outParams
        let modules =
            moduleMap
            |> Map.toList
            |> List.map snd
            |> List.sortDescending
        CppGenerator.generateProject modules outParams

        // TODO: move these into the CppGenerator module,
        // because we've discovered that statically linking between languages is a real problem,
        // and so the same language will have to be used for both Core and Impl
        DynlibGenerator.copySupportFiles outParams
        DynlibGenerator.generateProject outParams
    | _ ->
        failwithf "unsupported backend language: %A" backLang

    // core/DLL project

    // printfn "moduleMap: %A" moduleMap

    // finally write current run time for next time
    // (only do this at the very end, because if we have a crash, we've written a false time, and files won't be processed!)
    File.WriteAllText(lastRunTimeFilePath, thisRunTime.ToString("O"))

let outParams =
    { Path = projectInfo.OutputPath
      MaybeNamespace = Some projectInfo.Namespace // more of a prefix, really - still will inlcude LibraryName at the end
      LibraryName = projectInfo.LibraryName }
appRunner projectInfo.InputPath projectInfo.Inputs outParams projectInfo.FrontLangs projectInfo.BackLang
