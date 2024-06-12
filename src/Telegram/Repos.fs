module Telegram.Repos

open System.Threading.Tasks
open Domain.Core
open otsom.fs.Telegram.Bot.Core

[<RequireQualifiedAccess>]
module PresetRepo =
  type QueueGeneration = UserId -> PresetId -> Task<unit>