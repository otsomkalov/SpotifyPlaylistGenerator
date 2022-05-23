module Shared.AppEnv

open Database
open Microsoft.Extensions.Logging
open Shared.Bot
open Shared.Db
open Shared.Log
open Shared.Services
open Shared.Spotify
open Telegram.Bot

[<Struct>]
[<NoComparison>]
type AppEnv(logger: ILogger, bot: ITelegramBotClient, context: AppDbContext, provider: SpotifyClientProvider) =
  interface ILog with
    member _.Logger = logger

  interface IBot with
    member _.Bot = bot

  interface IDb with
    member _.Db = context

  interface ISpotify with
    member _.Provider = provider
