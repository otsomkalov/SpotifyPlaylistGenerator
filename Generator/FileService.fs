module FileService

open System.IO
open System.Text.Json
open Spotify

let saveIdsToFile filePath ids =
    task {
        let rawIds = ids |> List.map RawTrackId.value
        let json = JsonSerializer.Serialize(rawIds)

        do! File.WriteAllTextAsync(filePath, json)
    }

let private readIdsFromFile filePath =
    task {
        let! json = File.ReadAllTextAsync(filePath)

        let data =
            JsonSerializer.Deserialize<string list>(json)
            |> List.map RawTrackId.create

        return data
    }

let private refreshCachedIds filePath loadIdsFunc =
    task {
        let! ids = loadIdsFunc

        do! saveIdsToFile filePath ids

        return ids
    }

let loadIdsFromFile fileName loadIdsFunc refreshCache =
    match (refreshCache, File.Exists fileName) with
    | true, true -> refreshCachedIds fileName loadIdsFunc
    | true, false -> refreshCachedIds fileName loadIdsFunc
    | false, true -> readIdsFromFile fileName
    | false, false -> refreshCachedIds fileName loadIdsFunc
