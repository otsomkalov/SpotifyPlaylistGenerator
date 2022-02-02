module FileService

open System.IO
open System.Text.Json

let readIdsFromFile filePath =
    task {
        let! json = File.ReadAllTextAsync(filePath)
        return JsonSerializer.Deserialize<string seq>(json)
    }

let saveIdsToFile filePath ids =
    task {
        let json = JsonSerializer.Serialize(ids)
        do! File.WriteAllTextAsync(filePath, json)
    }
