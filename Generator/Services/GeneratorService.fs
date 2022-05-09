namespace Generator.Services

open System.Text.Json
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Shared.Services
open Shared.QueueMessages
open Generator.Extensions
open Generator.Domain
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
    _bot: ITelegramBotClient
  ) =
  let _amazonSettings = _amazonOptions.Value

  member this.GeneratePlaylistAsync(messageBody: string) =
    task {
      let queueMessage =
        JsonSerializer.Deserialize<GeneratePlaylistMessage>(messageBody)

      _logger.LogInformation("Received message to generate playlist for Spotify user with id {SpotifyUserId}", queueMessage.SpotifyId)

      (ChatId(queueMessage.TelegramId), "Generating playlist...")
      |> _bot.SendTextMessageAsync
      |> ignore

      let! likedTracksIds = _likedTracksService.ListIdsAsync queueMessage.TelegramId queueMessage.RefreshCache
      let! historyTracksIds = _historyPlaylistsService.ListTracksIdsAsync queueMessage.TelegramId queueMessage.RefreshCache
      let! playlistsTracksIds = _playlistsService.ListTracksIdsAsync queueMessage.TelegramId queueMessage.RefreshCache

      let tracksIdsToExclude =
        List.append likedTracksIds historyTracksIds

      _logger.LogInformation("Tracks to exclude count: {TracksToExcludeCount}", tracksIdsToExclude.Length)

      let potentialTracks =
        playlistsTracksIds
        |> List.except tracksIdsToExclude

      _logger.LogInformation("Potential tracks count: {PotentialTracksCount}", potentialTracks.Length)

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
