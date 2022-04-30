namespace Generator.Services

open System.IO
open System.Text.Json
open Generator
open Microsoft.Extensions.Logging

type FileService(_logger: ILogger<FileService>) =
    member _.saveIdsAsync fileName ids =
        task {
            let rawIds =
                ids |> List.map RawTrackId.value

            let json = JsonSerializer.Serialize(rawIds)

            do! File.WriteAllTextAsync(fileName, json)
        }

    member _.readIdsAsync fileName =
        task {
            let! json = File.ReadAllTextAsync(fileName)

            let data =
                JsonSerializer.Deserialize<string list>(json)
                |> List.map RawTrackId.create

            return data
        }

    member _.exists filePath = File.Exists filePath
