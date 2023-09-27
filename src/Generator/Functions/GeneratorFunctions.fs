﻿namespace Generator

open Generator.Bot
open Infrastructure.Workflows
open Infrastructure
open Microsoft.Azure.WebJobs
open Microsoft.Extensions.Logging
open Shared.QueueMessages
open Shared.Services
open StackExchange.Redis
open Domain.Workflows
open Domain.Core
open Telegram.Bot
open Generator.Extensions
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

      let userId = message.TelegramId |> UserId

      let listTracks = Playlist.listTracks logger client
      let listLikedTracks = User.listLikedTracks client
      let sendMessage = Telegram.sendMessage _bot userId
      let getRecommendations = Spotify.getRecommendations client

      let listPlaylistTracks = Cache.listOrRefresh playlistsCache message.RefreshCache listTracks

      let listLikedTracks =
        Cache.listOrRefreshByKey likedTracksCache message.RefreshCache listLikedTracks message.TelegramId

      let updateTargetedPlaylist = TargetedPlaylist.updateTracks playlistsCache client

      logger.LogInformation("Received request to generate playlist for user with Telegram id {TelegramId}", message.TelegramId)

      do! sendMessage "Generating playlist..."

      let generatePlaylist =
        Domain.Workflows.Playlist.generate logger listPlaylistTracks listLikedTracks loadPreset updateTargetedPlaylist List.shuffle getRecommendations

      let! generatePlaylistResult = generatePlaylist (message.PresetId |> PresetId) |> Async.StartAsTask

      let messageText =
        match generatePlaylistResult with
        | Ok _ -> "Playlist generated!"
        | Error(Playlist.GenerateError e) -> e

      return! sendMessage messageText
    }
