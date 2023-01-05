namespace Shared.Settings

open System

module ConnectionStrings =
  [<Literal>]
  let Postgre = "Postgre"

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

module AmazonSettings =
  [<Literal>]
  let SectionName = "Amazon"

type AmazonSettings() =
  member val QueueUrl = "" with get, set
