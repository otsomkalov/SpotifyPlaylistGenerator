module Infrastructure.Helpers

open System
open System.Text.Json
open System.Text.Json.Serialization
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

[<RequireQualifiedAccess>]
module Task =
  let tee f t =
    task{
      let! v = t

      do f v

      return v
    }

module JSON =
  let options =
    JsonFSharpOptions.Default().WithUnionExternalTag().WithUnionUnwrapRecordCases().ToJsonSerializerOptions()

  let serialize value =
    JsonSerializer.Serialize(value, options)

  let deserialize<'a> (json: string) =
    JsonSerializer.Deserialize<'a>(json, options)