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
    _spotifyClientProvider: SpotifyClientProvider,
    loadPreset: Preset.Load,
    _bot: ITelegramBotClient,
    connectionMultiplexer: IConnectionMultiplexer
  ) =

  [<FunctionName("GenerateAsync")>]
  member this.GenerateAsync([<QueueTrigger("%Storage:QueueName%")>] message: GeneratePlaylistMessage, logger: ILogger) =
    let playlistsCache = connectionMultiplexer.GetDatabase 0
    let likedTracksCache = connectionMultiplexer.GetDatabase 3

    task {
      let! client = _spotifyClientProvider.GetAsync message.TelegramId

      let listTracks = Playlist.listTracks logger client
      let listLikedTracks = User.listLikedTracks client

      let listPlaylistTracks = Cache.listOrRefresh playlistsCache message.RefreshCache listTracks

      let listLikedTracks =
        Cache.listOrRefreshByKey likedTracksCache message.RefreshCache listLikedTracks message.TelegramId

      let updateTargetPlaylist = TargetPlaylist.update playlistsCache client

      logger.LogInformation("Received request to generate playlist for user with Telegram id {TelegramId}", message.TelegramId)

      (ChatId(message.TelegramId), "Generating playlist...")
      |> _bot.SendTextMessageAsync
      |> ignore

      let generatePlaylist =
        Domain.Workflows.Playlist.generate logger listPlaylistTracks listLikedTracks loadPreset updateTargetPlaylist List.shuffle

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
