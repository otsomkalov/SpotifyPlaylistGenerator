module Infrastructure.Helpers

open System
open System.Threading.Tasks
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

module Spotify =
  let (|ApiException|_|) (ex: exn) =
    match ex with
    | :? AggregateException as aggregateException ->
      aggregateException.InnerExceptions
      |> Seq.tryPick (fun e -> e :?> APIException |> Option.ofObj)
    | :? APIException as e -> Some e
    | _ -> None

module ValueTask =
  let asTask (valueTask: ValueTask<'a>) = valueTask.AsTask()