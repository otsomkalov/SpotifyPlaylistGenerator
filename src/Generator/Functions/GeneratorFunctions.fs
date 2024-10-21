namespace Generator.Functions

open Domain.Extensions
open Domain.Repos
open FSharp
open Infrastructure.Repos
open Infrastructure
open Microsoft.ApplicationInsights
open Microsoft.Azure.Functions.Worker
open Microsoft.Extensions.Logging
open MongoDB.Driver
open SpotifyAPI.Web
open StackExchange.Redis
open Domain.Workflows
open Domain.Core
open Telegram.Bot
open otsom.fs.Core
open otsom.fs.Extensions
open otsom.fs.Telegram.Bot.Core

type RunEnv(telemetryClient, connectionMultiplexer, client, logger, userId) =
  interface IListPlaylistTracks with
    member this.ListPlaylistTracks(playlistId) =
      PlaylistRepo.listTracks telemetryClient connectionMultiplexer logger client playlistId

  interface IListLikedTracks with
    member this.ListLikedTracks() = UserRepo.listLikedTracks telemetryClient connectionMultiplexer client userId ()

type GeneratorFunctions
  (
    _db: IMongoDatabase,
    _bot: ITelegramBotClient,
    _logger: ILogger<GeneratorFunctions>,
    connectionMultiplexer: IConnectionMultiplexer,
    sendUserMessage: SendUserMessage,
    telemetryClient: TelemetryClient,
    getSpotifyClient: Spotify.GetClient,
    editBotMessage: EditBotMessage
  ) =

  [<Function("GenerateAsync")>]
  member this.GenerateAsync([<QueueTrigger("%Storage:QueueName%")>] command: {| UserId: UserId; PresetId: PresetId |}, _: FunctionContext) =
    let loadPreset = PresetRepo.load _db
    let getPreset = Preset.get loadPreset

    use _ =
      _logger.BeginScope(
        "Running playlist generation for user %i{TelegramId} and preset %s{PresetId}",
        (command.UserId |> UserId.value),
        (command.PresetId |> PresetId.value)
      )

    task {
      let! client = getSpotifyClient command.UserId |> Task.map Option.get

      let logRecommendedTracks =
        Logf.logfi
          _logger
          "Preset %s{PresetId} of user %i{TelegramId} has %i{RecommendedTracksCount} recommended tracks"
          (command.PresetId |> PresetId.value)
          (command.UserId |> UserId.value)

      let listTracks =
        PlaylistRepo.listTracks telemetryClient connectionMultiplexer _logger client

      let listExcludedTracks = PresetRepo.listExcludedTracks _logger listTracks

      let sendMessage = sendUserMessage command.UserId
      let getRecommendations = TrackRepo.getRecommendations logRecommendedTracks client

      let appendTracks =
        TargetedPlaylistRepo.appendTracks telemetryClient client connectionMultiplexer

      let replaceTracks =
        TargetedPlaylistRepo.replaceTracks telemetryClient client connectionMultiplexer

      do
        Logf.logfi _logger "Received request to generate playlist for user with Telegram id %i{TelegramId}" (command.UserId |> UserId.value)

      let io: Domain.Workflows.Preset.RunIO =
        { ListExcludedTracks = listExcludedTracks
          LoadPreset = getPreset
          AppendTracks = appendTracks
          ReplaceTracks = replaceTracks
          GetRecommendations = getRecommendations
          Shuffler = List.shuffle }

      let editMessage = editBotMessage command.UserId

      let env = RunEnv(telemetryClient, connectionMultiplexer, client, _logger, command.UserId)

      let runPreset = Domain.Workflows.Preset.run env io
      let runPreset = Telegram.Workflows.Preset.run sendMessage editMessage runPreset

      do! runPreset command.PresetId
    }
