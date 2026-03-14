module SupportCopier

open System
open System.IO

let private SUPPORT_ROOT =
    Path.Combine(AppContext.BaseDirectory, "support-files")
    
type CustomCopyFunc =
    // (sourceRoot, destRoot, relativePath)
    string -> string -> string -> unit

let private defaultCopyFunc (sourceBase: string) (destBase: string) (relativePath: string) =
    printfn "default copy: [%s] -> [%s] (file: [%s])" sourceBase destBase relativePath
    // let sourceAbs = sourceBase + "/" + relativePath
    // let destAbs = destBase + "/" + relativePath
    // File.Copy
    
let replaceCopyFunc (replaceMap: Map<string, string>) (sourceBase: string) (destBase: string) (relativePath: string) =
    // printfn "replaceCopyFunc map: %A" replaceMap
    // printfn "REPLACE copy: [%s] -> [%s] (file: [%s])" sourceBase destBase relativePath
    let sourcePath = sourceBase + "/" + relativePath
    let destPath = destBase + "/" + relativePath
    let templateText = File.ReadAllText(sourcePath)
    let fullyReplaced =
        (templateText, replaceMap)
        ||> Map.fold (fun acc findMe replaceWith ->
            acc.Replace(findMe, replaceWith))
    printfn "== writing all text to: [%s]" destPath
    File.WriteAllText(destPath, fullyReplaced)

let copyFiles (sourceRoot: string) (destRoot: string) (createDest: bool)
    (globs: string list) (recursive: bool) (customCopyFunc: CustomCopyFunc option) =
        let sourceAbsolute = SUPPORT_ROOT + "/" + sourceRoot
        printfn "== copy support files =="
        printfn "- src root: [%s]" sourceAbsolute
        printfn "- dest root: [%s] (create: %s)" destRoot (if createDest then "true" else "false")
        printfn "- globs: %A" globs
        printfn "- recursive: %s" (if recursive then "true" else "false")

        // create destination
        Directory.CreateDirectory(destRoot) |> ignore
        
        // identify all files matching the globs
        let filesToCopy =
            globs
            |> Seq.collect (fun glob ->
                Directory.EnumerateFiles(sourceAbsolute, glob, SearchOption.AllDirectories))
            |> Seq.distinct // shouldn't be any overlap in most cases ...
            
        let copyFunc =
            customCopyFunc
            |> Option.defaultValue defaultCopyFunc
            
        printfn "=== files to copy: ==="
        for file in filesToCopy do
            let relativePath =
                Path.GetRelativePath(sourceAbsolute, file)
            copyFunc sourceAbsolute destRoot relativePath
