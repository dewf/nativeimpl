module DynlibGenerator

open System.IO
open AppCommon
open Generators.Common

let private projectDir (outParams: OutParams) =
    sprintf "%s/server/cpp/_dllproject" outParams.Path

let copySupportFiles (outParams: OutParams) =
    let destRoot =
        projectDir outParams
    // filesystem structure's a bit odd, nothing really set in stone yet
    SupportCopier.copyFiles "core" (destRoot + "/core") true ["*.cpp";"*.h"] false None
    // for windows:
    SupportCopier.copyFiles "core/platforms" destRoot true ["dllmain.cpp"] false None
    // for .NET, or any other platform that uses a raw DLL (vs. Python/Java/etc which require custom C++ code)
    let copyFunc =
        let replaceMap =
            let prefix =
                sprintf "%s_ni_" (Util.lower outParams.LibraryName)
            [ "%%DLL_EXPORT_PREFIX%%", prefix ]
            |> Map.ofList
        SupportCopier.replaceCopyFunc replaceMap
    SupportCopier.copyFiles "core/languages" destRoot true ["DirectExport.*"] false (Some copyFunc)

let private writeProject (writer: IFormatWriter) (outParams: OutParams) =
    let write, blank = writer.Write, writer.Blank
    write 0 "cmake_minimum_required(VERSION 4.2)"
    write 0 "project(%sServer)" outParams.LibraryName
    blank()
    write 0 "set(CMAKE_CXX_STANDARD 23)"
    blank()
    write 0 "set(CMAKE_POSITION_INDEPENDENT_CODE ON)"
    write 0 "set(CMAKE_CXX_VISIBILITY_PRESET hidden)"
    blank()
    write 0 "add_compile_definitions(DLLPROJECT_EXPORTS)"
    blank()
    write 0 "set(SOURCES"
    write 1 "core/NativeImplCore.h"
    write 1 "core/NativeImplCore.cpp"
    write 1 "DirectExport.h"
    write 1 "DirectExport.cpp)"
    blank()
    write 0 "if (WIN32)"
    write 1 "list(APPEND SOURCES dllmain.cpp)"
    write 0 "endif()"
    blank()
    write 0 "add_library(%sServer SHARED ${SOURCES})" outParams.LibraryName
    blank()
    write 0 "# link to actual library implementation"
    write 0 "add_subdirectory(../%sImpl ImplBuild)" outParams.LibraryName
    blank()
    write 0 "target_link_libraries(%sServer PRIVATE %sImpl)" outParams.LibraryName outParams.LibraryName

let generateProject (outParams: OutParams) =
    let filename =
        sprintf "%s/CMakeLists.txt" (projectDir outParams)
        |> safeFilename ".new"
    use output =
        let writer = new StreamWriter(filename)
        new SimpleFormatWriter(writer, 0)
    writeProject output outParams
