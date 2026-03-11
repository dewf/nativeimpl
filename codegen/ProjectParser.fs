module ProjectParser

open System.IO
open Tomlyn
open Tomlyn.Model

type ProjectInfo = {
    FrontLang: string
    BackLang: string
    LibraryName: string
    Namespace: string
    InputPath: string
    OutputPath: string
    Inputs: string list
}

let parseProjectInfo (tomlPath: string) =
    let content = File.ReadAllText(tomlPath)
    let root = TomlSerializer.Deserialize<TomlTable>(content)
    
    let projectTable =
        match root["project"] with
        | :? TomlTable as t -> t
        | _ -> failwith "Expected [project] section in TOML file"
        
    let frontLang =
        match projectTable["front_lang"] with
        | :? string as s -> s
        | _ -> failwith "TOML project missing 'front_lang' value [allowed values: \"csharp\"]"
        
    let backLang =
        match projectTable["back_lang"] with
        | :? string as s -> s
        | _ -> failwith "TOML project missing 'back_lang' value [allowed values: \"cpp\""
        
    let libraryName =
        match projectTable["library_name"] with
        | :? string as s -> s
        | _ -> failwith "TOML project missing 'library_name' value"
        
    let namespace_ =
        match projectTable["namespace"] with
        | :? string as s -> s
        | _ -> failwith "TOML project missing 'namespace' value"
        
    let inputPath =
        match projectTable["input_path"] with
        | :? string as s -> s
        | _ -> failwith "TOML project missing 'input_path' value"
    
    let outputPath =
        match projectTable["output_path"] with
        | :? string as s -> s
        | _ -> failwith "TOML project missing 'output_path' value"
        
    let inputs =
        match projectTable["inputs"] with
        | :? TomlArray as arr -> arr |> Seq.map string |> Seq.toList
        | _ -> failwith "TOML project missing 'inputs' value"
        
    { FrontLang = frontLang
      BackLang = backLang
      LibraryName = libraryName
      Namespace = namespace_
      InputPath = inputPath
      OutputPath = outputPath
      Inputs = inputs }
