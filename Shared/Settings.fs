namespace Shared.Settings

open System
open Microsoft.FSharp.Core

module SpotifySettings =
  [<Literal>]
  let SectionName = "Spotify"

[<CLIMutable>]
type SpotifySettings =
  { ClientId: string
    ClientSecret: string
    CallbackUrl: Uri }

module TelegramSettings =
  [<Literal>]
  let SectionName = "Telegram"

[<CLIMutable>]
type TelegramSettings = { Token: string; BotUrl: string }

module DatabaseSettings =
  [<Literal>]
  let SectionName = "Database"

[<CLIMutable>]
type DatabaseSettings = { ConnectionString: string }

module AmazonSettings =
  [<Literal>]
  let SectionName = "Amazon"

[<CLIMutable>]
type AmazonSettings = { QueueUrl: string }
