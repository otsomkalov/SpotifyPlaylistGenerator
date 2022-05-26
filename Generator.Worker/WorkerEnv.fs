module Generator.Worker.Env

open Amazon.SQS
open Database
open Generator.Worker.Log
open Microsoft.Extensions.Logging
open Shared.Bot
open Shared.Db
open Shared.SQS
open Shared.Services
open Shared.Settings
open Shared.Spotify
open Telegram.Bot

[<Struct>]
[<NoComparison>]
type WorkerEnv
  (
    logger: ILogger,
    bot: ITelegramBotClient,
    context: AppDbContext,
    provider: SpotifyClientProvider
  ) =
  interface ILog with
    member _.Logger = logger

  interface IBot with
    member _.Bot = bot

  interface IDb with
    member _.Db = context

  interface ISpotify with
    member _.Provider = provider