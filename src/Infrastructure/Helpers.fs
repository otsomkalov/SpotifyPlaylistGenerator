module Infrastructure.Helpers

open System
open SpotifyAPI.Web

[<RequireQualifiedAccess>]
module Task =
  let map mapping task' =
    task {
      let! value = task'

      return mapping value
    }

[<RequireQualifiedAccess>]
module TaskOption =
  let map mapping taskOption =
    task {
      let! option = taskOption

      return
        match option with
        | Some v -> mapping v |> Some
        | None -> None
    }

[<RequireQualifiedAccess>]
module Async =

  let inline singleton (value: 'value) : Async<'value> = value |> async.Return

  let inline bind ([<InlineIfLambda>] binder: 'input -> Async<'output>) (input: Async<'input>) : Async<'output> = async.Bind(input, binder)

  let inline map ([<InlineIfLambda>] mapper: 'input -> 'output) (input: Async<'input>) : Async<'output> =
    bind (fun x' -> mapper x' |> singleton) input

module Spotify =
  let (|ApiException|_|) (ex: exn) =
    match ex with
    | :? AggregateException as aggregateException ->
      aggregateException.InnerExceptions
      |> Seq.tryPick (fun e -> e :?> APIException |> Option.ofObj)
    | :? APIException as e -> Some e
    | _ -> None
