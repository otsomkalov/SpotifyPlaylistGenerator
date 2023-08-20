module Generator.Bot.Helpers

open System

let (|StartsWith|_|) (substring: string) (str: string) =
  if str.StartsWith(substring, StringComparison.InvariantCultureIgnoreCase) then
    Some()
  else
    None

let (|Equals|_|) (toCompare: string) (source: string) =
  if source = toCompare then
    Some()
  else
    None

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

let (|Int|_|) (str: string) =
  match Int32.TryParse(str) with
  | true, value -> Some(value)
  | _ -> None

let (|CallbackQueryData|_|) (str: string) =
  match str.Split("|") with
  | [| entityType; entityId; entityAction |] -> Some(entityType, entityId, entityAction)
  | _ -> None

let (|CallbackQueryDataWithPage|_|) (str: string) =
  match str.Split("|") with
  | [| entityType; entityId; entityAction; page |] -> Some(entityType, entityId, entityAction, int page)
  | _ -> None

let (|PresetAction|_|) (str: string) =
  match str.Split("|") with
  | [| entityType; entityId; entityAction |] ->
    match entityType, entityId with
    | "p", Int id -> Some(id, entityAction)
    | _ -> None
  | _ -> None