namespace Generator.Worker.Services

open System.IO
open System.Text.Json
open Generator.Worker.Domain
open Microsoft.Extensions.Logging

type FileService(_logger: ILogger<FileService>) =
  member _.SaveIdsAsync filePath ids =
    task {
      let rawIds =
        ids |> List.map RawTrackId.value

      let json = JsonSerializer.Serialize(rawIds)

      do! File.WriteAllTextAsync(filePath, json)
    }

  member _.ReadIdsAsync filePath =
    task {
      let! json = File.ReadAllTextAsync(filePath)

      let data =
        JsonSerializer.Deserialize<string list>(json)
        |> List.map RawTrackId.create

      return data
    }

  member _.Exists filePath = File.Exists filePath
