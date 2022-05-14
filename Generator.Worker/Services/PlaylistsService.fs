namespace Generator.Worker.Services

open Microsoft.Extensions.Logging
open System.Linq
open Microsoft.EntityFrameworkCore
open Shared.Data

type PlaylistsService(_playlistService: PlaylistService, _logger: ILogger<PlaylistsService>, _context: AppDbContext) =
  member _.ListTracksIdsAsync userId refreshCache =
    task {
      let! playlistsUrls =
        _context
          .Playlists
          .AsNoTracking()
          .Where(fun p ->
            p.UserId = userId
            && p.PlaylistType = PlaylistType.Source)
          .Select(fun p -> p.Url)
          .ToListAsync()

      let! tracksIds = _playlistService.ListTracksIdsAsync userId playlistsUrls refreshCache

      _logger.LogInformation(
        "User with Telegram id {TelegramId} has {PlaylistsTracksIdsCount} tracks in source playlists",
        userId,
        tracksIds.Length
      )

      return tracksIds
    }
