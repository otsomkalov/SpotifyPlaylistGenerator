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
module TaskResult =
  let ok v = Ok v |> Task.FromResult

  let error e = Error e |> Task.FromResult

  let bind binder taskResult =
    taskResult |> Task.map (Result.bind binder)

  let map mapping taskResult =
    taskResult |> Task.map (Result.map mapping)

  let taskMap mapping taskResult =
    taskResult |> Task.bind (Result.taskMap mapping)

  let mapError mapping taskResult =
    taskResult |> Task.map (Result.mapError mapping)

  let either onOk onError taskResult =
    taskResult |> Task.map (Result.either onOk onError)

[<RequireQualifiedAccess>]
module List =
  let shuffle sequence =
    let random = Random()

    sequence |> List.sortBy (fun _ -> random.Next())
