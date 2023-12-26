namespace Infrastructure.Settings

open System

module SpotifySettings =
  [<Literal>]
  let SectionName = "Spotify"

type SpotifySettings() =
  member val ClientId = "" with get, set
  member val ClientSecret = "" with get, set
  member val CallbackUrl: Uri = null with get, set

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

module RedisSettings =
  [<Literal>]
  let SectionName = "Redis"

type RedisSettings() =
  member val ConnectionString  = "" with get, set