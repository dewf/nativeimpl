module Generators.Frontend.DlangGenerator

open System.IO
open Generators.Common
open Parser
open AppCommon

type DlangRenderContext = {
    BaseNamespace: string list
    RenderContext: RenderContext
    OpaqueMethodContext: OpaqueInfo option         // rendering an opaque method (pop/invocation/push) at the current group/path, prone to extra collisions on the push/pop statements
} with
    member this.BaseNamespaceJoined =
        joinNameParts "." this.BaseNamespace
    member this.ModuleContext =
        this.RenderContext.ModuleContext
    member this.GroupContext =
        this.RenderContext.GroupContext
    member this.NavigateToSub (name: string) =
        let nextRenderContext =
            this.RenderContext.NavigateToSub name
        { this with RenderContext = nextRenderContext }

let writeModule (moduleContext: ModuleContext) (writer: IFormatWriter) (baseNamespace: string) (libraryName: string) (maybeModulePrefix: string option) (simpleName: string) =
    let write, blank = writer.Write, writer.Blank
    let m =
        moduleContext.Module
    let renderContext =
        { BaseNamespace = (baseNamespace.Split "." |> Array.toList)
          RenderContext = createRenderContext moduleContext
          OpaqueMethodContext = None }
    let root =
        renderContext.GroupContext

    let fullyQualifiedModulename =
        let prefix =
            maybeModulePrefix
            |> Option.map (fun prefix -> libraryName + "." + prefix)
            |> Option.defaultValue libraryName
        prefix + "." + simpleName
        |> _.ToLower()
    write 0 "module %s;" fullyQualifiedModulename

let libraryName (outParams: OutParams) =
    outParams.LibraryName.ToLower()

let projectDir (outParams: OutParams) =
    sprintf "%s/client/dlang/%s" outParams.Path (libraryName outParams)

let generateModule (context: ModuleContext) (outParams: OutParams) =
    let baseNamespace, maybeModulePrefix, simpleName =
        namespaceParts outParams context.Module.Name
    let moduleDir =
        let subPath =
            maybeModulePrefix
            |> Option.map (fun mp -> "/" + mp.Replace(".", "/"))
            |> Option.defaultValue ""
        sprintf "%s/source/%s%s" (projectDir outParams) (libraryName outParams) subPath
    Directory.CreateDirectory(moduleDir) |> ignore
    let filename =
        sprintf "%s/%s.d" moduleDir (simpleName.ToLower())
    let modulePrefixString =
        maybeModulePrefix
        |> Option.map (fun mp -> $"{mp}.")
        |> Option.defaultValue ""
    printfn "Dlang writing (%s.%s%s) [%s]" baseNamespace modulePrefixString simpleName filename
    use output =
        let writer = new StreamWriter(filename)
        new SimpleFormatWriter(writer, 0)
    writeModule context output baseNamespace (libraryName outParams) maybeModulePrefix simpleName

let generateProject (outParams: OutParams) =
    printfn "Dlang.generateProject: TODO"

let generateLibraryFile (allModules: Module list) (outParams: OutParams) =
    printfn "Dlang.generateLibraryFile: TODO"

let copySupportFiles (outParams: OutParams) =
    printfn "Dlang.copySupportFiles: TODO"
