open System
open System.IO
open Generators.Backend
open Generators.Frontend
open Parser
open AppCommon

let FRONT_LANG = "csharp"
let BACK_LANG = "cpp"

// // KITCHENSINK
// let LIBRARY = "KitchenSink"
// let NAMESPACE = "Org.Prefixed"
// let INPUT_PATH = @"C:\DataPart\Development\nativeimpl\kitchensink"
// let OUTPUT_PATH = @"C:\DataPart\Development\nativeimpl\kitchensink\_output"
// let INPUTS = [ "First"; "Second" ]

// // TESTBENCH
// let LIBRARY = "TestBench"
// let NAMESPACE = "Org.Prefixed"
// let INPUT_PATH = @"D:\Development\testbench\constexpr"
// let OUTPUT_PATH = @"D:\Development\testbench\constexpr\_output"
// let INPUTS = [ "First" ]

// // GUIBASE
// let LIBRARY = "GuiBase"
// let NAMESPACE = "Org.Prefixed"
// let INPUT_PATH = @"C:\DataPart\Development\guibase"
// let OUTPUT_PATH = @"C:\DataPart\Development\guibase\output"
// let INPUTS = [ "Windowing"; "Keys"; "Drawing"; "Text"; ]

// // MinimalQtForFSharp
// let LIBRARY = "MinimalQtForFSharp" // note, a .DLL of this will be created, so if you are wrapping a native library, you will want a distinct name to avoid collisions (eg when wrapping a C++ library BigLibrary, call it BigLibrarySharp or -Wrapper or something)
// let NAMESPACE = "Org.Whatever"
// let INPUT_PATH = @"D:\Development\FSharpQt\MinimalQtForFSharp\interfaces"
// let OUTPUT_PATH = @"D:\Development\FSharpQt\MinimalQtForFSharp"

// MinimalQt
let LIBRARY = "MinimalQt" // note, a .DLL of this will be created, so if you are wrapping a native library, you will want a distinct name to avoid collisions (eg when wrapping a C++ library BigLibrary, call it BigLibrarySharp or -Wrapper or something)
let NAMESPACE = "Org.Whatever"
let INPUT_PATH = @"D:\Development\MinimalQt\interfaces"
let OUTPUT_PATH = @"D:\Development\MinimalQt"
let INPUTS = [
    "Action"
    "AbstractButton"
    "AbstractItemDelegate"
    "AbstractItemModel"
    "AbstractItemView"
    "AbstractListModel"
    "AbstractProxyModel"
    "AbstractScrollArea"
    "AbstractSlider"
    "AbstractSpinBox"
    "Application"
    "BoxLayout"
    "Brush"
    "CalendarWidget"
    "CheckBox"
    "Color"
    "ComboBox"
    "Common"
    "Completer"
    "CoreApplication"
    "Cursor"
    "Date"
    "DateTime"
    "Dialog"
    "Dir"
    "DockWidget"
    "Drag"
    "Enums"
    "Event"
    "FileDialog"
    "Font"
    "FormLayout"
    "Frame"
    "Gradient"
    "GraphicsEffect"
    "GridLayout"
    "GroupBox"
    "GuiApplication"
    "HeaderView"
    "Icon"
    "Image"
    "ItemSelection"
    "ItemSelectionCommon"
    "ItemSelectionModel"
    "KeySequence"
    "Label"
    "Layout"
    "LayoutItem"
    "LineEdit"
    "ListView"
    "MainWindow"
    "Menu"
    "MenuBar"
    "MessageBox"
    "MimeData"
    "ModelIndex"
    "Object"
    "PaintDevice"
    "Painter"
    "PainterPath"
    "Pen"
    "PersistentModelIndex"
    "Pixmap"
    "PlainTextEdit"
    "Polygon"
    "PolygonF"
    "ProgressBar"
    "PushButton"
    "RadioButton"
    "Region"
    "RegularExpression"
    "SafeReturn"
    "Screen"
    "ScrollArea"
    "ScrollBar"
    "SessionManager"
    "SizePolicy"
    "Slider"
    "SortFilterProxyModel"
    "SpinBox"
    "Splitter"
    "SplitterHandle"
    "StandardItem"
    "StandardItemModel"
    "StatusBar"
    "StyledItemDelegate"
    "StyleOption"
    "StyleOptionViewItem"
    "TabBar"
    "TabWidget"
    "TextCharFormat"
    "TextCursor"
    "TextDocument"
    "TextEdit"
    "TextFormat"
    "TextOption"
    "Timer"
    "ToolBar"
    "Transform"
    "TreeView"
    "Url"
    "Validator"
    "Variant"
    "Widget"
    "Window"
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
    // read last run time from disk
    let lastRunTimeFilePath =
        INPUT_PATH + @"/__last_run_time.txt"
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
    
    // finally write current run time for next time
    // (only do this at the very end, because if we have a crash, we've written a false time, and files won't be processed!)
    File.WriteAllText(lastRunTimeFilePath, thisRunTime.ToString("O"))

let outParams =
    { Path = OUTPUT_PATH; MaybeNamespace = Some NAMESPACE; LibraryName = LIBRARY }
appRunner INPUT_PATH INPUTS outParams FRONT_LANG BACK_LANG
