namespace Generator.Functions

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
  member this.GenerateAsync([<QueueTrigger("%Storage:QueueName%")>] command: {| UserId: UserId; PresetId: PresetId |}, _: FunctionContext) =
    let loadPreset = PresetRepo.load _db
    let loadUser = UserRepo.load _db
    let getPreset = Preset.get loadPreset

    use _ =
      _logger.BeginScope(
        "Running playlist generation for user %i{TelegramId} and preset %s{PresetId}",
        (command.UserId |> UserId.value),
        (command.PresetId |> PresetId.value)
      )

    task {
      let! client = _spotifyClientProvider.GetAsync command.UserId

      let logRecommendedTracks =
        Logf.logfi
          _logger
          "Preset %s{PresetId} of user %i{TelegramId} has %i{RecommendedTracksCount} recommended tracks"
          (command.PresetId |> PresetId.value)
          (command.UserId |> UserId.value)

      let listTracks =
        PlaylistRepo.listTracks telemetryClient connectionMultiplexer _logger client

      let listIncludedTracks = PresetRepo.listIncludedTracks _logger listTracks

      let listExcludedTracks = PresetRepo.listExcludedTracks _logger listTracks

      let listLikedTracks =
        UserRepo.listLikedTracks telemetryClient connectionMultiplexer client _logger command.UserId

      let sendMessage = sendUserMessage command.UserId
      let getRecommendations = TrackRepo.getRecommendations logRecommendedTracks client

      let appendTracks =
        TargetedPlaylistRepo.appendTracks telemetryClient client connectionMultiplexer

      let replaceTracks =
        TargetedPlaylistRepo.replaceTracks telemetryClient client connectionMultiplexer

      do
        Logf.logfi _logger "Received request to generate playlist for user with Telegram id %i{TelegramId}" (command.UserId |> UserId.value)

      let io: Domain.Workflows.Preset.GenerateIO =
        { ListIncludedTracks = listIncludedTracks
          ListExcludedTracks = listExcludedTracks
          ListLikedTracks = listLikedTracks
          LoadPreset = getPreset
          AppendTracks = appendTracks
          ReplaceTracks = replaceTracks
          GetRecommendations = getRecommendations }

      let generatePreset = Domain.Workflows.Preset.generate io

      let generateCurrentPreset =
        Domain.Workflows.User.generateCurrentPreset loadUser generatePreset

      let generateCurrentPreset =
        Telegram.Workflows.User.generateCurrentPreset sendMessage generateCurrentPreset

      return! generateCurrentPreset command.UserId
    }
