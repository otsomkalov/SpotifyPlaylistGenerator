module List

open System

let shuffle sequence =
    let random = Random()

    sequence |> List.sortBy (fun _ -> random.Next())
