namespace Shared.Settings

open System

type RedisSettings() =
  member val ConnectionString  = "" with get, set

module RedisSettings =
  [<Literal>]
  let SectionName = "Redis"

module SpotifySettings =
  [<Literal>]
  let SectionName = "Spotify"

type SpotifySettings() =
  member val ClientId = "" with get, set
  member val ClientSecret = "" with get, set
  member val CallbackUrl: Uri = null with get, set

module TelegramSettings =
  [<Literal>]
  let SectionName = "Telegram"

type TelegramSettings() =
  member val Token = "" with get, set
  member val BotUrl = "" with get, set

module DatabaseSettings =
  [<Literal>]
  let SectionName = "Database"

type DatabaseSettings() =
  member val ConnectionString = "" with get, set

module StorageSettings =
  [<Literal>]
  let SectionName = "Storage"

type StorageSettings() =
  member val ConnectionString = "" with get, set

  member val QueueName = "" with get, set