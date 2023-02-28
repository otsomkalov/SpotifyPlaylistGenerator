namespace Generator.Worker.Services

open System
open System.Collections.Generic
open System.Linq
open System.Text.Json
open Database
open Database.Entities
open Generator.Worker.Domain
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Caching.Distributed
open Microsoft.Extensions.Logging
open Shared.Services
open SpotifyAPI.Web

type HistoryPlaylistsService
  (
    _playlistService: PlaylistService,
    _spotifyClientProvider: SpotifyClientProvider,
    _cache: IDistributedCache,
    _logger: ILogger<HistoryPlaylistsService>,
    _context: AppDbContext
  ) =

  member _.ListTracksIdsAsync userId refreshCache =
    task {
      let! historyPlaylistsUrls =
        _context
          .Playlists
          .AsNoTracking()
          .Where(fun p -> p.UserId = userId && p.PlaylistType = PlaylistType.History)
          .Select(fun p -> p.Url)
          .ToListAsync()

      let! tracksIds = _playlistService.ListTracksIdsAsync userId historyPlaylistsUrls refreshCache

      _logger.LogInformation(
        "User with Telegram id {TelegramId} has {HistoryPlaylistsTracksCount} tracks in history playlists",
        userId,
        tracksIds.Length
      )

      return tracksIds
    }
