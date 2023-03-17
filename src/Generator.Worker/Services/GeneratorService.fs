namespace Generator.Worker.Services


open Database
open Microsoft.Extensions.Logging
open Shared.Services
open Shared.QueueMessages
open Generator.Worker.Extensions
open Telegram.Bot
open Telegram.Bot.Types
open Microsoft.EntityFrameworkCore

type GeneratorService
  (
    _likedTracksService: LikedTracksService,
    _historyPlaylistsService: HistoryPlaylistsService,
    _targetPlaylistService: TargetPlaylistService,
    _spotifyClientProvider: SpotifyClientProvider,
    _playlistsService: PlaylistsService,
    _logger: ILogger<GeneratorService>,
    _bot: ITelegramBotClient,
    _context: AppDbContext
  ) =

  member this.GeneratePlaylistAsync(queueMessage: GeneratePlaylistMessage) =
    task {
      _logger.LogInformation("Received request to generate playlist for user with Telegram id {TelegramId}", queueMessage.TelegramId)

      (ChatId(queueMessage.TelegramId), "Generating playlist...")
      |> _bot.SendTextMessageAsync
      |> ignore

      let! user =
        _context
          .Users
          .AsNoTracking()
          .FirstOrDefaultAsync(fun u -> u.Id = queueMessage.TelegramId)

      let! likedTracksIds = _likedTracksService.ListIdsAsync queueMessage.TelegramId queueMessage.RefreshCache
      let! historyTracksIds = _historyPlaylistsService.ListTracksIdsAsync queueMessage.TelegramId queueMessage.RefreshCache
      let! playlistsTracksIds = _playlistsService.ListTracksIdsAsync queueMessage.TelegramId queueMessage.RefreshCache

      let excludedTracksIds, includedTracksIds =
        match user.Settings.IncludeLikedTracks |> Option.ofNullable with
        | Some v when v = true -> historyTracksIds, playlistsTracksIds @ likedTracksIds
        | Some v when false -> likedTracksIds @ historyTracksIds, playlistsTracksIds
        | None -> historyTracksIds, playlistsTracksIds

      _logger.LogInformation(
        "User with Telegram id {TelegramId} has {TracksToExcludeCount} tracks to exclude",
        queueMessage.TelegramId,
        excludedTracksIds.Length
      )

      let potentialTracksIds =
        includedTracksIds
        |> List.except excludedTracksIds

      _logger.LogInformation(
        "User with Telegram id {TelegramId} has {PotentialTracksCount} potential tracks",
        queueMessage.TelegramId,
        potentialTracksIds.Length
      )

      let tracksIdsToImport =
        potentialTracksIds
        |> List.shuffle
        |> List.take user.Settings.PlaylistSize

      do! _targetPlaylistService.SaveTracksAsync queueMessage.TelegramId tracksIdsToImport
      do! _targetPlaylistService.UpdateCachedAsync queueMessage.TelegramId tracksIdsToImport

      (ChatId(queueMessage.TelegramId), "Playlist generated!")
      |> _bot.SendTextMessageAsync
      |> ignore
    }
