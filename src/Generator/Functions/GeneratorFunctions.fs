namespace Generator.Functions

open System.Threading.Tasks
open FSharp
open Infrastructure.Repos
open Infrastructure.Telegram.Services
open Infrastructure
open Microsoft.ApplicationInsights
open Microsoft.Azure.Functions.Worker
open Microsoft.Extensions.Logging
open MongoDB.Driver
open StackExchange.Redis
open Domain.Workflows
open Domain.Core
open Telegram.Bot
open otsom.fs.Telegram.Bot.Core
open otsom.fs.Extensions

type GeneratorFunctions
  (
    _db: IMongoDatabase,
    _spotifyClientProvider: SpotifyClientProvider,
    _bot: ITelegramBotClient,
    _logger: ILogger<GeneratorFunctions>,
    connectionMultiplexer: IConnectionMultiplexer,
    sendUserMessage: SendUserMessage,
    telemetryClient: TelemetryClient
  ) =

  [<Function("GenerateAsync")>]
  member this.GenerateAsync([<QueueTrigger("%Storage:QueueName%")>] command: {| UserId: UserId; PresetId: PresetId |}, _: FunctionContext) =
    let playlistsCache = connectionMultiplexer.GetDatabase Cache.playlistsDatabase
    let likedTracksCache = connectionMultiplexer.GetDatabase Cache.likedTracksDatabase

    let loadPreset = PresetRepo.load _db
    let getPreset = Preset.get loadPreset

    task {
      let! client = _spotifyClientProvider.GetAsync command.UserId

      let logIncludedTracks =
        Logf.logfi
          _logger
          "Preset %s{PresetId} of user %i{TelegramId} has %i{IncludedTracksCount} included tracks"
          (command.PresetId |> PresetId.value)
          (command.UserId |> UserId.value)

      let logExcludedTracks =
        Logf.logfi
          _logger
          "Preset %s{PresetId} of user %i{TelegramId} has %i{ExcludedTracksCount} excluded tracks"
          (command.PresetId |> PresetId.value)
          (command.UserId |> UserId.value)

      let logRecommendedTracks =
        Logf.logfi
          _logger
          "Preset %s{PresetId} of user %i{TelegramId} has %i{RecommendedTracksCount} recommended tracks"
          (command.PresetId |> PresetId.value)
          (command.UserId |> UserId.value)

      let listTracks = Spotify.Playlist.listTracks _logger client
      let listTracks = Cache.Playlist.listTracks telemetryClient playlistsCache listTracks

      let listIncludedTracks = PresetRepo.listIncludedTracks logIncludedTracks listTracks

      let listExcludedTracks = PresetRepo.listExcludedTracks logExcludedTracks listTracks

      let listLikedTracks =
        UserRepo.listLikedTracks telemetryClient likedTracksCache client _logger command.UserId

      let sendMessage = sendUserMessage command.UserId
      let getRecommendations = TrackRepo.getRecommendations logRecommendedTracks client

      let appendTracksInSpotify = TargetedPlaylistRepo.appendTracksInSpotify client
      let replaceTracksInSpotify = TargetedPlaylistRepo.replaceTracksInSpotify client

      let appendTracksInCache = TargetedPlaylistRepo.appendTracksInCache playlistsCache
      let replaceTracksInCache = TargetedPlaylistRepo.replaceTracksInCache playlistsCache

      do
        Logf.logfi _logger "Received request to generate playlist for user with Telegram id %i{TelegramId}" (command.UserId |> UserId.value)

      let io: Domain.Workflows.Preset.GenerateIO =
        { ListIncludedTracks = listIncludedTracks
          ListExcludedTracks = listExcludedTracks
          ListLikedTracks = listLikedTracks
          LoadPreset = getPreset
          AppendTracks =
            fun a b ->
              Task.WhenAll([| appendTracksInSpotify a b; appendTracksInCache a b |])
              |> Task.ignore
          ReplaceTracks =
            fun a b ->
              Task.WhenAll([| replaceTracksInSpotify a b; replaceTracksInCache a b |])
              |> Task.ignore
          GetRecommendations = getRecommendations }

      let generatePlaylist = Domain.Workflows.Preset.generate io

      let generatePlaylist =
        Telegram.Workflows.Playlist.generate sendMessage generatePlaylist

      return! generatePlaylist command.PresetId
    }
