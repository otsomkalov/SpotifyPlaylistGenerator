module Generator.Worker.Services.FileService

open System.IO
open System.Text.Json
open Shared.Spotify

let saveIdsAsync fileName ids =
  task {
    let rawIds =
      ids |> List.map RawTrackId.value

    let json = JsonSerializer.Serialize(rawIds)

    do! File.WriteAllTextAsync(fileName, json)
  }

let readIdsAsync fileName =
  task {
    let! json = File.ReadAllTextAsync(fileName)

    let data =
      JsonSerializer.Deserialize<string list>(json)
      |> List.map RawTrackId.create

    return data
  }
