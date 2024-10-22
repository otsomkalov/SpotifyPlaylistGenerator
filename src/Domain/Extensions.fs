module Domain.Extensions

open System

[<RequireQualifiedAccess>]
module List =
  let shuffle sequence =
    let random = Random()

    sequence |> List.sortBy (fun _ -> random.Next())

  let errorIfEmpty (error: 'e) =
    function
    | [] -> Error error
    | v -> Ok v

[<RequireQualifiedAccess>]
module Result =
  let errorIf condition (error: 'e) =
    fun arg -> if (condition arg) then Error error else Ok arg
