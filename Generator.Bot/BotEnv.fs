module Generator.Bot.Env

open Amazon.SQS
open Database
open Generator.Bot.ExtendedSpotify
open Microsoft.Extensions.Options
open Shared.Bot
open Shared.Db
open Shared.SQS
open Shared.Services
open Shared.Settings
open Shared.Spotify
open Telegram.Bot

[<Struct>]
[<NoComparison>]
type BotEnv
  (
    bot: ITelegramBotClient,
    context: AppDbContext,
    provider: SpotifyClientProvider,
    sqs: IAmazonSQS,
    amazonSettings: IOptions<AmazonSettings>,
    spotifySettings: IOptions<SpotifySettings>
  ) =
  interface IBot with
    member _.Bot = bot

  interface IDb with
    member _.Db = context

  interface IExtendedSpotify with
    member _.Settings = spotifySettings.Value
    member _.Provider = provider

  interface ISQS with
    member _.Settings = amazonSettings.Value
    member _.SQS = sqs
