namespace Generator.Functions

open FSharp
open Infrastructure.Telegram.Services
open Infrastructure.Workflows
open Infrastructure
open Microsoft.Azure.Functions.Worker
open Microsoft.Extensions.Logging
open StackExchange.Redis
open Domain.Workflows
open Domain.Core
open Telegram.Bot

type GeneratorFunctions
  (
    _spotifyClientProvider: SpotifyClientProvider,
    loadPreset: Preset.Load,
    _bot: ITelegramBotClient,
    _logger: ILogger<GeneratorFunctions>,
    connectionMultiplexer: IConnectionMultiplexer
  ) =

  [<Function("GenerateAsync")>]
  member this.GenerateAsync([<QueueTrigger("%Storage:QueueName%")>] command: {|PresetId: PresetId|}, _: FunctionContext) =
    let playlistsCache = connectionMultiplexer.GetDatabase Cache.playlistsDatabase
    let likedTracksCache = connectionMultiplexer.GetDatabase Cache.likedTracksDatabase

    task {
      let! preset = loadPreset command.PresetId

      let! client = _spotifyClientProvider.GetAsync preset.UserId

      let logIncludedTracks =
        Logf.logfi _logger
          "Preset %s{PresetId} of user %i{TelegramId} has %i{IncludedTracksCount} included tracks"
          (command.PresetId |> PresetId.value) (preset.UserId |> UserId.value)

      let logExcludedTracks =
        Logf.logfi _logger
          "Preset %s{PresetId} of user %i{TelegramId} has %i{ExcludedTracksCount} excluded tracks"
          (command.PresetId |> PresetId.value) (preset.UserId |> UserId.value)

      let logLikedTracks =
        Logf.logfi _logger
          "User %i{TelegramId} has %i{LikedTracksCount} liked tracks"
          (preset.UserId |> UserId.value)

      let logRecommendedTracks =
        Logf.logfi _logger
          "Preset %s{PresetId} of user %i{TelegramId} has %i{RecommendedTracksCount} recommended tracks"
          (command.PresetId |> PresetId.value) (preset.UserId |> UserId.value)

      let logPotentialTracks =
        Logf.logfi _logger
          "Preset %s{PresetId} of user %i{TelegramId} has %i{PotentialTracksCount} potential tracks"
          (command.PresetId |> PresetId.value) (preset.UserId |> UserId.value)

      let listTracks = Spotify.Playlist.listTracks _logger client
      let listTracks = Cache.Playlist.listTracks _logger playlistsCache listTracks
      let listLikedTracks = Spotify.User.listLikedTracks client

      let listIncludedTracks =
        Workflows.Preset.listIncludedTracks  logIncludedTracks listTracks

      let listExcludedTracks =
        Workflows.Preset.listExcludedTracks logExcludedTracks listTracks

      let listLikedTracks =
        Cache.User.listLikedTracks likedTracksCache logLikedTracks listLikedTracks preset.UserId

      let sendMessage = Telegram.Workflows.sendMessage _bot preset.UserId
      let getRecommendations = Spotify.getRecommendations logRecommendedTracks client

      let updateTargetedPlaylist = TargetedPlaylist.updateTracks playlistsCache client

      do Logf.logfi _logger "Received request to generate playlist for user with Telegram id %i{TelegramId}" (preset.UserId |> UserId.value)

      let io: Domain.Workflows.Playlist.GenerateIO =
        { LogPotentialTracks = logPotentialTracks
          ListIncludedTracks = listIncludedTracks
          ListExcludedTracks = listExcludedTracks
          ListLikedTracks = listLikedTracks
          LoadPreset = loadPreset
          UpdateTargetedPlaylists = updateTargetedPlaylist
          GetRecommendations = getRecommendations }

      let generatePlaylist = Domain.Workflows.Playlist.generate io
      let generatePlaylist = Telegram.Workflows.Playlist.generate sendMessage generatePlaylist

      return! generatePlaylist command.PresetId
    }
