namespace Generator

open System.Threading.Tasks
open Infrastructure.Workflows
open Infrastructure
open Microsoft.Azure.WebJobs
open Microsoft.Extensions.Logging
open Shared.QueueMessages
open Shared.Services
open StackExchange.Redis
open Generator.Worker.Services
open Domain.Workflows

type GeneratorFunctions
  (
    _logger: ILogger<GeneratorFunctions>,
    _generatorService: GeneratorService,
    _cache: IDatabase,
    _spotifyClientProvider: SpotifyClientProvider,
    loadPreset: Preset.Load
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

    _generatorService.GeneratePlaylistAsync(message, listPlaylistTracks, listLikedTracks, updateTargetPlaylist, loadPreset)
    |> Async.StartAsTask
    :> Task
