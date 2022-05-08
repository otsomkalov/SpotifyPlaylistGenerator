namespace Generator.Services

open System.Collections.Generic
open System.Linq
open EntityFrameworkCore.FSharp
open Generator.Domain
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Logging
open Shared.Data
open Shared.Services
open SpotifyAPI.Web
open EntityFrameworkCore.FSharp.DbContextHelpers

type HistoryPlaylistsService
  (
    _playlistService: PlaylistService,
    _spotifyClientProvider: SpotifyClientProvider,
    _fileService: FileService,
    _logger: ILogger<HistoryPlaylistsService>,
    _context: AppDbContext
  ) =

  member _.ListTracksIdsAsync userId refreshCache =
    task {
      _logger.LogInformation("Listing history playlists tracks ids")

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

      _logger.LogInformation("History playlists tracks count: {HistoryPlaylistsTracksCount}", tracksIds.Length)

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
        _spotifyClientProvider.GetClient userId

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

      return! _fileService.SaveIdsAsync $"{targetHistoryPlaylistId}.json" tracksIds
    }
