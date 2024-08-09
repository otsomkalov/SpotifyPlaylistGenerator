namespace Generator.Functions

open Domain.Extensions
open System.Diagnostics
open FSharp
open Infrastructure.Queue
open Infrastructure.Repos
open Infrastructure.Telegram.Repos
open Infrastructure.Telegram.Services
open Infrastructure
open Microsoft.ApplicationInsights
open Microsoft.ApplicationInsights.DataContracts
open Microsoft.Azure.Functions.Worker
open Microsoft.Extensions.Logging
open MongoDB.Driver
open StackExchange.Redis
open Domain.Workflows
open Telegram.Bot
open otsom.fs.Core
open otsom.fs.Telegram.Bot.Core

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
  member this.GenerateAsync([<QueueTrigger("%Storage:QueueName%")>] message: BaseMessage<PresetRepo.GeneratePresetRequest>, _: FunctionContext) =
    let loadPreset = PresetRepo.load _db
    let loadUser = UserRepo.load _db
    let getPreset = Preset.get loadPreset

    let request = message.Data

    use _ =
      _logger.BeginScope(
        "Running playlist generation for user %i{TelegramId} and preset %s{PresetId}",
        (request.UserId |> UserId.value),
        (request.PresetId |> PresetId.value)
      )

    task {

      use activity =
        (new Activity("GeneratePreset")).SetParentId(message.OperationId)

      use operation = telemetryClient.StartOperation<RequestTelemetry>(activity)

      let! client = _spotifyClientProvider.GetAsync request.UserId

      let logRecommendedTracks =
        Logf.logfi
          _logger
          "Preset %s{PresetId} of user %i{TelegramId} has %i{RecommendedTracksCount} recommended tracks"
          (request.PresetId |> PresetId.value)
          (request.UserId |> UserId.value)

      let listTracks =
        PlaylistRepo.listTracks telemetryClient connectionMultiplexer _logger client

      let listIncludedTracks = PresetRepo.listIncludedTracks _logger listTracks

      let listExcludedTracks = PresetRepo.listExcludedTracks _logger listTracks

      let listLikedTracks =
        UserRepo.listLikedTracks telemetryClient connectionMultiplexer client _logger request.UserId

      let sendMessage = sendUserMessage request.UserId
      let getRecommendations = TrackRepo.getRecommendations logRecommendedTracks client

      let appendTracks =
        TargetedPlaylistRepo.appendTracks telemetryClient client connectionMultiplexer

      let replaceTracks =
        TargetedPlaylistRepo.replaceTracks telemetryClient client connectionMultiplexer

      do
        Logf.logfi _logger "Received request to generate playlist for user with Telegram id %i{TelegramId}" (request.UserId |> UserId.value)

      let io: Preset.GenerateIO =
        { ListIncludedTracks = listIncludedTracks
          ListExcludedTracks = listExcludedTracks
          ListLikedTracks = listLikedTracks
          LoadPreset = getPreset
          AppendTracks = appendTracks
          ReplaceTracks = replaceTracks
          GetRecommendations = getRecommendations
          Shuffler = List.shuffle }

      let generatePreset = Preset.generate io

      let generateCurrentPreset =
        User.generateCurrentPreset loadUser generatePreset

      let generateCurrentPreset =
        Telegram.Workflows.User.generateCurrentPreset sendMessage generateCurrentPreset

      do! generateCurrentPreset request.UserId

      operation.Telemetry.Success <- true

      return ()
    }
