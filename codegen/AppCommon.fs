module AppCommon

open System.IO

type OutParams = {
    Path: string
    MaybeNamespace: string option
    LibraryName: string
    ClientLibraryName: string
}

let safeFilename (suffixIfExists: string) (defaultPath: string)  =
    if File.Exists defaultPath then
        defaultPath + suffixIfExists
    else
        defaultPath
