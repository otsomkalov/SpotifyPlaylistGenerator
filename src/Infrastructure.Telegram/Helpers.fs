module Infrastructure.Telegram.Helpers

open System
open System.Text.Json
open System.Text.Json.Serialization
open otsom.fs.Extensions

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

module JSON =
  let options =
    JsonFSharpOptions.Default()
        .ToJsonSerializerOptions()

  let serialize obj =
    JsonSerializer.Serialize(obj, options)