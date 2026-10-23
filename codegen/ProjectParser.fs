module ProjectParser

open System.IO
open System.Linq
open Tomlyn
open Tomlyn.Model

type Language =
    | Cpp
    | CSharp
    | Dlang
with
    static member FromString (str: string) =
        match str.ToLower() with
        | "c++" | "cpp" -> Cpp
        | "c#" | "csharp" -> CSharp
        | "d" | "dlang" -> Dlang
        | _ ->
            failwithf "unrecognized langauge name: [%s]" str

type ProjectInfo = {
    ProjectFileLocation: string
    FrontLangs: Language list
    BackLang: Language
    LibraryName: string
    ClientLibrarySuffix: string option
    Namespace: string
    InputPath: string
    OutputPath: string
    Inputs: string list
}

let tomlTryGet (table: TomlTable) key =
    if table.ContainsKey key then
        Some table[key]
    else
        None

let parseProjectInfo (tomlPath: string) =
    let content = File.ReadAllText(tomlPath)
    let root = TomlSerializer.Deserialize<TomlTable>(content)

    let projectFileLocation =
        tomlPath
        |> Path.GetFullPath
        |> Path.GetDirectoryName

    let projectTable =
        match root["project"] with
        | :? TomlTable as t -> t
        | _ -> failwith "Expected [project] section in TOML file"

    let frontLangs =
        match tomlTryGet projectTable "front_langs" with
        | Some (:? TomlArray as items) ->
            items.Cast<string>()
            |> Seq.map Language.FromString
            |> Seq.toList
        // | Some x ->
        //     x.GetType()
        //     |> failwithf "TOML project file: `front_langs` was wrong type (needs to be array of strings, was: %A)"
        | _ ->
            match tomlTryGet projectTable "front_lang" with
            | Some (:? string as s) -> [ Language.FromString s ]
            | _ -> failwith "TOML project missing valid 'front_lang' or 'front_langs' value [available languages: \"csharp\", \"dlang\"]"

    let backLang =
        match projectTable["back_lang"] with
        | :? string as s ->
            Language.FromString s
        | _ -> failwith "TOML project missing 'back_lang' value [allowed values: \"cpp\""

    let libraryName =
        match projectTable["library_name"] with
        | :? string as s -> s
        | _ -> failwith "TOML project missing 'library_name' value"

    let clientLibrarySuffix =
        match tomlTryGet projectTable "client_library_suffix" with
        | Some (:? string as s) -> Some s
        | _ -> None

    let namespace_ =
        match projectTable["namespace"] with
        | :? string as s -> s
        | _ -> failwith "TOML project missing 'namespace' value"

    let inputPath =
        match projectTable["input_path"] with
        | :? string as s ->
            if Path.IsPathFullyQualified s then
                s
            else
                Path.Combine(projectFileLocation, s)
                |> Path.GetFullPath
        | _ -> failwith "TOML project missing 'input_path' value"

    let outputPath =
        match projectTable["output_path"] with
        | :? string as s ->
            if Path.IsPathFullyQualified s then
                s
            else
                Path.Combine(projectFileLocation, s)
                |> Path.GetFullPath
        | _ -> failwith "TOML project missing 'output_path' value"

    let inputs =
        match projectTable["inputs"] with
        | :? TomlArray as arr -> arr |> Seq.map string |> Seq.toList
        | _ -> failwith "TOML project missing 'inputs' value"

    { ProjectFileLocation = projectFileLocation
      FrontLangs = frontLangs
      BackLang = backLang
      LibraryName = libraryName
      ClientLibrarySuffix = clientLibrarySuffix
      Namespace = namespace_
      InputPath = inputPath
      OutputPath = outputPath
      Inputs = inputs }
