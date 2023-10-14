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
    task{
      let! value = task'

      return! mapping value
    }

  let taskMap (mapping: 'a -> Task<'b>) task' =
    task {
      let! value = task'

      return! mapping value
    }

[<RequireQualifiedAccess>]
module Result =
  let ofOption error option =
    match option with
    | Some value -> Ok value
    | None -> Error error

  let taskMap mappingTask result =
    match result with
    | Ok v ->
      task {
        let! value = mappingTask v
        return Ok value
      }
    | Error e -> Error e |> Task.FromResult

  let asyncMap mapping result =
    match result with
    | Ok v ->
      async {
        let! value = mapping v
        return Ok value
      }
    | Error e -> Error e |> async.Return

  let taskBind binder result =
    match result with
    | Error e -> Error e |> Task.FromResult
    | Ok x -> binder x

  let asyncBind binder result =
    match result with
    | Error e -> Error e |> async.Return
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
    task {
      let! result = taskResult

      return!
        match result with
        | Error e -> Error e |> Task.FromResult
        | Ok x -> binder x
    }

  let map mapping taskResult =
    task {
      let! result = taskResult
      return Result.map mapping result
    }

  let taskMap mapping taskResult =
    task {
      let! result = taskResult
      return! Result.taskMap mapping result
    }

  let mapError mapping taskResult =
    task {
      let! result = taskResult

      return
        match result with
        | Error e -> mapping e |> Error
        | Ok x -> Ok x
    }

[<RequireQualifiedAccess>]
module Async =

  let inline singleton (value: 'value) : Async<'value> = value |> async.Return

  let inline bind ([<InlineIfLambda>] binder: 'input -> Async<'output>) (input: Async<'input>) : Async<'output> = async.Bind(input, binder)

  let inline map ([<InlineIfLambda>] mapper: 'input -> 'output) (input: Async<'input>) : Async<'output> =
    bind (fun x' -> mapper x' |> singleton) input

[<RequireQualifiedAccess>]
module AsyncResult =

  let inline retn (value: 'ok) : Async<Result<'ok, 'error>> = Ok value |> Async.singleton

  let inline ok (value: 'ok) : Async<Result<'ok, 'error>> = retn value

  let inline returnError (error: 'error) : Async<Result<'ok, 'error>> = Error error |> Async.singleton

  let inline error (error: 'error) : Async<Result<'ok, 'error>> = returnError error

  let inline map ([<InlineIfLambda>] mapper: 'input -> 'output) (input: Async<Result<'input, 'error>>) : Async<Result<'output, 'error>> =
    Async.map (Result.map mapper) input

  let inline mapError
    ([<InlineIfLambda>] mapper: 'inputError -> 'outputError)
    (input: Async<Result<'ok, 'inputError>>)
    : Async<Result<'ok, 'outputError>> =
    Async.map (Result.mapError mapper) input

  let inline bind
    ([<InlineIfLambda>] binder: 'input -> Async<Result<'output, 'error>>)
    (input: Async<Result<'input, 'error>>)
    : Async<Result<'output, 'error>> =
    Async.bind (Result.either binder returnError) input

  let inline bindSync
    ([<InlineIfLambda>] binder: 'input -> Result<'output, 'error>)
    (input: Async<Result<'input, 'error>>)
    : Async<Result<'output, 'error>> =
    Async.bind (Result.either (binder >> Async.singleton) returnError) input

  let asyncMap mapping taskResult =
    async {
      let! result = taskResult
      return! Result.asyncMap mapping result
    }

[<RequireQualifiedAccess>]
module List =
  let shuffle sequence =
    let random = Random()

    sequence |> List.sortBy (fun _ -> random.Next())