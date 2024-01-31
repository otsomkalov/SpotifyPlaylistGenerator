namespace Infrastructure.Settings

open System

[<CLIMutable>]
type SpotifySettings =
  { ClientId: string
    ClientSecret: string
    CallbackUrl: Uri }

  static member SectionName = "Telegram"

[<CLIMutable>]
type DatabaseSettings =
  { ConnectionString: string
    Name: string }

  static member SectionName = "Database"

[<CLIMutable>]
type StorageSettings =
  { ConnectionString: string
    QueueName: string }

  static member SectionName = "Storage"

[<CLIMutable>]
type RedisSettings =
  { ConnectionString: string }

  static member SectionName = "Redis"
