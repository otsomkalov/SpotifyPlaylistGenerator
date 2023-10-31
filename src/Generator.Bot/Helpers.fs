module Generator.Bot.Helpers

open System
open Domain.Core

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

let (|Bool|_|) (str: string) =
  match bool.TryParse(str) with
  | true, value -> Some(value)
  | _ -> None
