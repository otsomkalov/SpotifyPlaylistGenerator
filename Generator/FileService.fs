module FileService

open System.IO
open System.Text.Json
open Spotify

let readIdsFromFile filePath =
    task {
        let! json = File.ReadAllTextAsync(filePath)

        return
            JsonSerializer.Deserialize<string seq>(json)
            |> Seq.map RawTrackId.create
    }

let saveIdsToFile filePath oldIds newIds =
    task {
        let oldStringIds = oldIds |> Seq.map RawTrackId.value

        let newStringIds =
            newIds
            |> Seq.map SpotifyTrackId.rawValue
            |> Seq.map RawTrackId.value

        let ids = Seq.append oldStringIds newStringIds

        let json = JsonSerializer.Serialize(ids)
        do! File.WriteAllTextAsync(filePath, json)

        return Some()
    }
