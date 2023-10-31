module Domain.Extensions

open System
open System.Threading.Tasks

[<RequireQualifiedAccess>]
module Task =
  let map mapping task' =
    task {
      let! value = task'

      return mapping value
    }

  let bind mapping task' =
    task {
      let! value = task'

      return! mapping value
    }

  let taskMap (mapping: 'a -> Task<'b>) task' =
    task {
      let! value = task'

      return! mapping value
    }

[<RequireQualifiedAccess>]
module Option =
  let taskMap (mapping: 'a -> Task<'b>) option =
    match option with
    | Some v -> mapping v |> Task.map Some
    | None -> None |> Task.FromResult

[<RequireQualifiedAccess>]
module Result =
  let ofOption error option =
    match option with
    | Some value -> Ok value
    | None -> Error error

  let taskMap mappingTask result =
    match result with
    | Ok v -> mappingTask v |> Task.map Ok
    | Error e -> Error e |> Task.FromResult

  let taskBind binder result =
    match result with
    | Error e -> Error e |> Task.FromResult
    | Ok x -> binder x

  let inline either
    ([<InlineIfLambda>] onOk: 'okInput -> 'output)
    ([<InlineIfLambda>] onError: 'errorInput -> 'output)
    (input: Result<'okInput, 'errorInput>)
    : 'output =
    match input with
    | Ok x -> onOk x
    | Error err -> onError err

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
