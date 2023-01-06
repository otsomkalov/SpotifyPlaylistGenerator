namespace Generator.Worker.Services

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
          .Where(fun p ->
            p.UserId = userId
            && p.PlaylistType = PlaylistType.History)
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

  member _.UpdateAsync (userId: int64) tracksIds =
    task {
      _logger.LogInformation("Adding new tracks to history playlist")

      let addItemsRequest =
        tracksIds
        |> List.map SpotifyTrackId.value
        |> List<string>
        |> PlaylistAddItemsRequest

      printfn "Saving tracks to history playlist"

      let client =
        _spotifyClientProvider.Get userId

      let! targetHistoryPlaylistId =
        _context
          .Playlists
          .AsNoTracking()
          .Where(fun p ->
            p.UserId = userId
            && p.PlaylistType = PlaylistType.TargetHistory)
          .Select(fun p -> p.Url)
          .FirstOrDefaultAsync()

      let! _ = client.Playlists.AddItems(targetHistoryPlaylistId, addItemsRequest)

      return ()
    }

  member _.UpdateCachedAsync userId tracksIds =
    task {
      _logger.LogInformation("Updating history playlist cache file")

      let! targetHistoryPlaylistId =
        _context
          .Playlists
          .AsNoTracking()
          .Where(fun p ->
            p.UserId = userId
            && p.PlaylistType = PlaylistType.TargetHistory)
          .Select(fun p -> p.Url)
          .FirstOrDefaultAsync()

      return! _cache.SetStringAsync (targetHistoryPlaylistId, JsonSerializer.Serialize tracksIds)
    }
