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
    write 0 "set(CMAKE_CXX_STANDARD 20)"
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
    write 0 "add_library(%sImpl STATIC IMPORTED GLOBAL)" outParams.LibraryName
    write 0 "set_target_properties(%sImpl PROPERTIES" outParams.LibraryName
    write 2 "IMPORTED_LOCATION_DEBUG"
    write 2 "\"${CMAKE_CURRENT_SOURCE_DIR}/../%sImpl/cmake-build-debug/%sImpl.lib\"" outParams.LibraryName outParams.LibraryName
    blank()
    write 2 "IMPORTED_LOCATION_RELEASE"
    write 2 "\"${CMAKE_CURRENT_SOURCE_DIR}/../%sImpl/cmake-build-release/%sImpl.lib\"" outParams.LibraryName outParams.LibraryName
    blank()
    write 2 "MAP_IMPORTED_CONFIG_RELWITHDEBINFO Release"
    write 2 "MAP_IMPORTED_CONFIG_MINSIZEREL Release"
    write 0 ")"
    write 0 "target_link_libraries(%sServer PRIVATE %sImpl)" outParams.LibraryName outParams.LibraryName

let generateProject (outParams: OutParams) =
    let filename =
        sprintf "%s/CMakeLists.txt" (projectDir outParams)
        |> safeFilename ".new"
    use output =
        let writer = new StreamWriter(filename)
        new SimpleFormatWriter(writer, 0)
    writeProject output outParams
