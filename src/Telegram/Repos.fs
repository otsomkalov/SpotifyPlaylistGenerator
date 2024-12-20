module Telegram.Repos

open System.Threading.Tasks
open Domain.Core
open otsom.fs.Core
open otsom.fs.Telegram.Bot.Core

[<RequireQualifiedAccess>]
module PresetRepo =
  type QueueGeneration = UserId -> PresetId -> Task<unit>

type SendLink = string -> string -> string -> Task<BotMessageId>