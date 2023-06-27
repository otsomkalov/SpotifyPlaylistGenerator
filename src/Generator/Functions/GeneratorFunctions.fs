namespace Generator

open Infrastructure.Workflows
open Infrastructure
open Microsoft.Azure.WebJobs
open Microsoft.Extensions.Logging
open Shared.QueueMessages
open Shared.Services
open StackExchange.Redis
open Domain.Workflows
open Domain.Core
open Infrastructure.Helpers
open Generator.Worker.Extensions
open Telegram.Bot
open Telegram.Bot.Types
open Domain.Extensions

type GeneratorFunctions
  (
    _logger: ILogger<GeneratorFunctions>,
    _cache: IDatabase,
    _spotifyClientProvider: SpotifyClientProvider,
    loadPreset: Preset.Load,
    _bot: ITelegramBotClient
  ) =

  [<FunctionName("GenerateAsync")>]
  member this.GenerateAsync([<QueueTrigger("%Storage:QueueName%")>] message: GeneratePlaylistMessage) =
    let client = _spotifyClientProvider.Get message.TelegramId

    let listTracks = Playlist.listTracks _logger client
    let listLikedTracks = User.listLikedTracks client

    let listPlaylistTracks = Cache.listOrRefresh _cache message.RefreshCache listTracks

    let listLikedTracks =
      Cache.listOrRefreshByKey _cache message.RefreshCache listLikedTracks message.TelegramId

    let updateTargetPlaylist = TargetPlaylist.update _cache client

    _logger.LogInformation("Received request to generate playlist for user with Telegram id {TelegramId}", message.TelegramId)

    (ChatId(message.TelegramId), "Generating playlist...")
    |> _bot.SendTextMessageAsync
    |> ignore

    let generatePlaylist =
      Domain.Workflows.Playlist.generate listPlaylistTracks listLikedTracks loadPreset updateTargetPlaylist List.shuffle

    task {
      let! generatePlaylistResult = generatePlaylist (message.PresetId |> PresetId) |> Async.StartAsTask

      let messageText =
        match generatePlaylistResult with
        | Ok _ -> "Playlist generated!"
        | Error(Playlist.GenerateError e) -> e

      return!
        (ChatId(message.TelegramId), messageText)
        |> _bot.SendTextMessageAsync
        |> Task.map ignore
    }
