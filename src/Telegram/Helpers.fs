module Telegram.Helpers

open System

[<RequireQualifiedAccess>]
module List =
  let takeSafe count list =
    if list |> List.length < count then list else list |> List.take count