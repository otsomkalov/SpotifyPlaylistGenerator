module Telegram.Repos

open System.Threading.Tasks
open Domain.Core
open otsom.fs.Core

[<RequireQualifiedAccess>]
module PresetRepo =
  type QueueGeneration = UserId -> PresetId -> Task<unit>

type SendLink = string -> string -> string -> Task<unit>