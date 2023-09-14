module Telegram.Helpers

open System

let (|Int|_|) (str: string) =
  match Int32.TryParse(str) with
  | true, value -> Some(value)
  | _ -> None

[<RequireQualifiedAccess>]
module List =
  let takeSafe count list =
    if list |> List.length < count then list else list |> List.take count