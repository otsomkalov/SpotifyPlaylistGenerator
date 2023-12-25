module Infrastructure.Telegram.Helpers

open System
open System.Text.Json
open System.Text.Json.Serialization
open otsom.FSharp.Extensions

let (|StartsWith|_|) (substring: string) (str: string) =
  if str.StartsWith(substring, StringComparison.InvariantCultureIgnoreCase) then
    Some()
  else
    None

let (|Equals|_|) (toCompare: string) (source: string) =
  if source = toCompare then Some() else None

let (|CommandWithData|_|) (command: string) (input: string) =
  match input.Split(" ") with
  | [| inputCommand; data |] -> if inputCommand = command then Some(data) else None
  | _ -> None

let (|CommandData|_|) (command: string) =
  let commandParts = command.Split(" ")

  if commandParts.Length > 1 then
    Some(commandParts |> Seq.last)
  else
    None

let (|Uri|_|) (str: string) =
  match Uri.TryCreate(str, UriKind.Absolute) with
  | true, uri -> Some(uri)
  | _ -> None

let (|Int|_|) (str: string) =
  match Int32.TryParse(str) with
  | true, value -> Some(value)
  | _ -> None

let (|Bool|_|) (str: string) =
  match bool.TryParse(str) with
  | true, value -> Some(value)
  | _ -> None

[<RequireQualifiedAccess>]
module TaskResult =
  open Domain.Extensions
  open System.Threading.Tasks

  let inline taskEither
    ([<InlineIfLambda>] onOk: 'okInput -> Task<'output>)
    ([<InlineIfLambda>] onError: 'errorInput -> Task<'output>)
    =
    Task.bind (Result.either onOk onError)

module JSON =
  let options =
    JsonFSharpOptions.Default()
        .ToJsonSerializerOptions()

  let serialize obj =
    JsonSerializer.Serialize(obj, options)