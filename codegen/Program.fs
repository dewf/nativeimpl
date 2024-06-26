open Generators.Backend
open Generators.Frontend
open Parser
open AppCommon

let FRONT_LANG = "csharp"
let BACK_LANG = "cpp"

// // TESTBENCH
// let LIBRARY = "TestBench"
// let NAMESPACE = "Org.Prefixed"
// let INPUT_PATH = @"C:\DataPart\Development\testbench"
// let OUTPUT_PATH = @"C:\DataPart\Development\testbench\_output"
// let INPUTS = [ "First" ]

// // KITCHENSINK
// let LIBRARY = "KitchenSink"
// let NAMESPACE = "Org.Prefixed"
// let INPUT_PATH = @"C:\DataPart\Development\nativeimpl\kitchensink"
// let OUTPUT_PATH = @"C:\DataPart\Development\nativeimpl\kitchensink\_output"
// let INPUTS = [ "First"; "Second" ]

// // GUIBASE
// let LIBRARY = "GuiBase"
// let NAMESPACE = "Org.Prefixed"
// let INPUT_PATH = @"C:\DataPart\Development\guibase"
// let OUTPUT_PATH = @"C:\DataPart\Development\guibase\output"
// let INPUTS = [ "Windowing"; "Keys"; "Drawing"; "Text"; ]

// Helm
let LIBRARY = "QtTesting"
let NAMESPACE = "Org.Whatever"
let INPUT_PATH = @"C:\DataPart\Development\Helm"
let OUTPUT_PATH = @"C:\DataPart\Development\Helm\output"
let INPUTS = [
    "Action"
    "AbstractButton"
    "AbstractItemModel"
    "AbstractItemView"
    "AbstractListModel"
    "AbstractProxyModel"
    "AbstractScrollArea"
    "AbstractSlider"
    "Application"
    "BoxLayout"
    "ComboBox"
    "Common"
    "Dialog"
    "DockWidget"
    "Enums"
    "FileDialog"
    "Frame"
    "GridLayout"
    "GroupBox"
    "Icon"
    "KeySequence"
    "Label"
    "Layout"
    "LineEdit"
    "ListView"
    "MainWindow"
    "Menu"
    "MenuBar"
    "MessageBox"
    "ModelIndex"
    "Object"
    "Painter"
    "PaintResources"
    "PlainTextEdit"
    "ProgressBar"
    "PushButton"
    "RadioButton"
    "RegularExpression"
    "ScrollArea"
    "Slider"
    "SortFilterProxyModel"
    "StatusBar"
    "TabWidget"
    "Timer"
    "ToolBar"
    "TreeView"
    "Variant"
    "Widget"
]

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
            
            nextMap)

    printfn "======= generating library files ======="
    // get dependency order first
    let depsOrder = getDependencyOrder moduleMap
    // frontend
    match frontLang.ToLower() with
    | "csharp" | "c#" ->
        printfn "rendering C# library file"
        CsharpGenerator.generateLibraryFile depsOrder outParams
    | _ ->
        failwithf "unsupported frontend language: %s" frontLang
        
    // backend
    match backLang.ToLower() with
    | "cpp" | "c++" ->
        printfn "rendering C++ library file"
        CppGenerator.generateLibrary depsOrder outParams
    | _ ->
        failwithf "unsupported backend language: %s" backLang
    
    // generate project files:
    CsharpGenerator.generateProject outParams
    // generate project files for backend (eg cmake)
    
    // printfn "moduleMap: %A" moduleMap

let outParams =
    { Path = OUTPUT_PATH; MaybeNamespace = Some NAMESPACE; LibraryName = LIBRARY }
appRunner INPUT_PATH INPUTS outParams FRONT_LANG BACK_LANG
