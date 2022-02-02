module Seq

open System

let shuffle sequence =
    let random = Random()

    sequence |> Seq.sortBy (fun _ -> random.Next())
