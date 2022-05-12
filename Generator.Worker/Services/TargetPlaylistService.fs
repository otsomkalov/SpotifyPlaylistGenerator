namespace Generator.Worker.Services

open System.Collections.Generic
open Generator.Worker.Domain
open Microsoft.Extensions.Logging
open Shared.Data
open Shared.Services
open SpotifyAPI.Web
open System.Linq
open Microsoft.EntityFrameworkCore

type TargetPlaylistService
  (
    _playlistService: PlaylistService,
    _spotifyClientProvider: SpotifyClientProvider,
    _logger: ILogger<TargetPlaylistService>,
    _context: AppDbContext
  ) =
  member _.SaveTracksAsync (userId: int64) tracksIds =
    task {
      _logger.LogInformation("Saving tracks ids to target playlist")

      let replaceItemsRequest =
        tracksIds
        |> List.map SpotifyTrackId.value
        |> List<string>
        |> PlaylistReplaceItemsRequest

      printfn "Saving tracks to target playlist"

      let client =
        _spotifyClientProvider.Get userId

      let! targetPlaylistId =
        _context
          .Playlists
          .Where(fun x ->
            x.UserId = userId
            && x.PlaylistType = PlaylistType.Target)
          .Select(fun x -> x.Url)
          .FirstOrDefaultAsync()

      let! _ = client.Playlists.ReplaceItems(targetPlaylistId, replaceItemsRequest)

      return ()
    }
