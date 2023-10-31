namespace Generator

open Domain
open FSharp
open Generator.Bot
open Infrastructure.Helpers
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
    let likedTracksCache = connectionMultiplexer.GetDatabase 1

    task {
      let! client = _spotifyClientProvider.GetAsync message.TelegramId

      let userId = message.TelegramId |> UserId

      let logIncludedTracks =
        Logf.logfi logger
          "Preset %s{PresetId} of user %i{TelegramId} has %i{IncludedTracksCount} included tracks"
          message.PresetId message.TelegramId

      let logExcludedTracks =
        Logf.logfi logger
          "Preset %s{PresetId} of user %i{TelegramId} has %i{ExcludedTracksCount} excluded tracks"
          message.PresetId message.TelegramId

      let logLikedTracks =
        Logf.logfi logger
          "User %i{TelegramId} has %i{LikedTracksCount} liked tracks"
          message.TelegramId

      let logRecommendedTracks =
        Logf.logfi logger
          "Preset %s{PresetId} of user %i{TelegramId} has %i{RecommendedTracksCount} recommended tracks"
          message.PresetId message.TelegramId

      let logPotentialTracks =
        Logf.logfi logger
          "Preset %s{PresetId} of user %i{TelegramId} has %i{PotentialTracksCount} potential tracks"
          message.PresetId message.TelegramId

      let listTracks = Spotify.Playlist.listTracks logger client
      let listTracks = Cache.Playlist.listTracks playlistsCache listTracks
      let listLikedTracks = Spotify.User.listLikedTracks client

      let listIncludedTracks =
        Workflows.Preset.listIncludedTracks  logIncludedTracks listTracks

      let listExcludedTracks =
        Workflows.Preset.listExcludedTracks logExcludedTracks listTracks

      let listLikedTracks =
        Cache.User.listLikedTracks likedTracksCache logLikedTracks listLikedTracks userId

      let sendMessage = Telegram.sendMessage _bot userId
      let getRecommendations = Spotify.getRecommendations logRecommendedTracks client

      let updateTargetedPlaylist = TargetedPlaylist.updateTracks playlistsCache client

      do Logf.logfi logger "Received request to generate playlist for user with Telegram id %i{TelegramId}" message.TelegramId

      do! sendMessage "Generating playlist..."

      let io: Domain.Workflows.Playlist.GenerateIO =
        { LogPotentialTracks = logPotentialTracks
          ListIncludedTracks = listIncludedTracks
          ListExcludedTracks = listExcludedTracks
          ListLikedTracks = listLikedTracks
          LoadPreset = loadPreset
          UpdateTargetedPlaylists = updateTargetedPlaylist
          GetRecommendations = getRecommendations }

      let generatePlaylist = Domain.Workflows.Playlist.generate io

      let! generatePlaylistResult = generatePlaylist (message.PresetId |> PresetId)

      return!
        match generatePlaylistResult with
        | Ok _ -> sendMessage "Playlist generated!"
        | Error(Playlist.GenerateError.NoPotentialTracks) -> sendMessage "Playlists combination in your preset produced 0 potential tracks"
    }
