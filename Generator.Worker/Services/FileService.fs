namespace Generator.Worker.Services

open System.IO
open System.Text.Json
open Generator.Worker.Domain
open Microsoft.Extensions.Logging

type FileService(_logger: ILogger<FileService>) =
  let getFilePath fileName =
    [|Path.GetTempPath(); fileName|]
    |> Path.Combine

  member _.SaveIdsAsync fileName ids =
    task {
      let rawIds =
        ids |> List.map RawTrackId.value

      let json = JsonSerializer.Serialize(rawIds)

      do! File.WriteAllTextAsync(getFilePath fileName, json)
    }

  member _.ReadIdsAsync fileName =
    task {
      let! json = File.ReadAllTextAsync(getFilePath fileName)

      let data =
        JsonSerializer.Deserialize<string list>(json)
        |> List.map RawTrackId.create

      return data
    }

  member _.Exists filePath = File.Exists filePath
