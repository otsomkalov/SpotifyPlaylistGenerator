module Domain.Extensions

open System
open System.Threading.Tasks
open otsom.fs.Extensions

[<RequireQualifiedAccess>]
module Option =
  let taskMap (mapping: 'a -> Task<'b>) option =
    match option with
    | Some v -> mapping v |> Task.map Some
    | None -> None |> Task.FromResult

[<RequireQualifiedAccess>]
module List =
  let shuffle sequence =
    let random = Random()

    sequence |> List.sortBy (fun _ -> random.Next())
