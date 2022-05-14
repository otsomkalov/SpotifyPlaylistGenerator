namespace Generator.Worker.Services

open System.Text.Json
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Shared.Data
open Shared.Services
open Shared.QueueMessages
open Generator.Worker.Extensions
open Generator.Worker.Domain
open Shared.Settings
open Telegram.Bot
open Telegram.Bot.Types

type GeneratorService
  (
    _likedTracksService: LikedTracksService,
    _historyPlaylistsService: HistoryPlaylistsService,
    _targetPlaylistService: TargetPlaylistService,
    _spotifyClientProvider: SpotifyClientProvider,
    _playlistsService: PlaylistsService,
    _logger: ILogger<GeneratorService>,
    _amazonOptions: IOptions<AmazonSettings>,
    _bot: ITelegramBotClient,
    _context: AppDbContext
  ) =
  let _amazonSettings = _amazonOptions.Value

  member this.GeneratePlaylistAsync(messageBody: string) =
    task {
      let queueMessage =
        JsonSerializer.Deserialize<GeneratePlaylistMessage>(messageBody)

      _logger.LogInformation("Received request to generate playlist for user with Telegram id {TelegramId}", queueMessage.TelegramId)

      (ChatId(queueMessage.TelegramId), "Generating playlist...")
      |> _bot.SendTextMessageAsync
      |> ignore

      let! likedTracksIds = _likedTracksService.ListIdsAsync queueMessage.TelegramId queueMessage.RefreshCache
      let! historyTracksIds = _historyPlaylistsService.ListTracksIdsAsync queueMessage.TelegramId queueMessage.RefreshCache
      let! playlistsTracksIds = _playlistsService.ListTracksIdsAsync queueMessage.TelegramId queueMessage.RefreshCache

      let tracksIdsToExclude =
        List.append likedTracksIds historyTracksIds

      _logger.LogInformation(
        "User with Telegram id {TelegramId} has {TracksToExcludeCount} tracks to exclude",
        queueMessage.TelegramId,
        tracksIdsToExclude.Length
      )

      let potentialTracks =
        playlistsTracksIds
        |> List.except tracksIdsToExclude

      _logger.LogInformation(
        "User with Telegram id {TelegramId} has {PotentialTracksCount} potential tracks",
        queueMessage.TelegramId,
        potentialTracks.Length
      )

      let tracksIdsToImport =
        potentialTracks
        |> List.shuffle
        |> List.take 20
        |> List.map SpotifyTrackId.create

      do! _targetPlaylistService.SaveTracksAsync queueMessage.TelegramId tracksIdsToImport
      do! _historyPlaylistsService.UpdateAsync queueMessage.TelegramId tracksIdsToImport

      let newHistoryTracksIds =
        tracksIdsToImport
        |> List.map SpotifyTrackId.rawValue
        |> List.append historyTracksIds

      do! _historyPlaylistsService.UpdateCachedAsync queueMessage.TelegramId newHistoryTracksIds

      (ChatId(queueMessage.TelegramId), "Playlist generated!")
      |> _bot.SendTextMessageAsync
      |> ignore
    }
