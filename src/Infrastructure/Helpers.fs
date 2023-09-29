module Infrastructure.Helpers

open System
open System.Threading.Tasks
open SpotifyAPI.Web

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