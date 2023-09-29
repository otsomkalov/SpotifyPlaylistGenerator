namespace Shared.Settings

open System

type RedisSettings() =
  member val ConnectionString  = "" with get, set

module RedisSettings =
  [<Literal>]
  let SectionName = "Redis"

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
  member val Name = "" with get, set

module StorageSettings =
  [<Literal>]
  let SectionName = "Storage"

type StorageSettings() =
  member val ConnectionString = "" with get, set

  member val QueueName = "" with get, set