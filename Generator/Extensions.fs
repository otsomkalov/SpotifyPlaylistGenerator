module Generator.Extensions

open System

module List =
  let shuffle sequence =
    let random = Random()

    sequence |> List.sortBy (fun _ -> random.Next())
