namespace Generator.Worker.Services

open System
open System.Collections.Generic
open System.Text.Json
open System.Threading.Tasks
open Database
open Database.Entities
open Generator.Worker.Domain
open Microsoft.Extensions.Caching.Distributed
open Microsoft.Extensions.Logging
open Shared.Services
open SpotifyAPI.Web
open System.Linq
open Microsoft.EntityFrameworkCore

type TargetPlaylistService
  (
    _playlistService: PlaylistService,
    _spotifyClientProvider: SpotifyClientProvider,
    _logger: ILogger<TargetPlaylistService>,
    _context: AppDbContext,
    _cache: IDistributedCache
  ) =
  member _.SaveTracksAsync (userId: int64) tracksIds =
    task {
      _logger.LogInformation("Saving tracks ids to target playlists")

      let! targetPlaylists =
        _context
          .TargetPlaylists
          .AsNoTracking()
          .Where(fun p -> p.PlaylistType = PlaylistType.Target)
          .ToListAsync()

      printfn "Saving tracks to target playlist"

      let client = _spotifyClientProvider.Get userId

      return!
        targetPlaylists
        |> Seq.map (fun playlist ->
          let mapSpotifyTrackId = List.map SpotifyTrackId.value >> List<string>

          if playlist.Overwrite then
            let replaceItemsRequest =
              tracksIds |> mapSpotifyTrackId |> PlaylistReplaceItemsRequest

            client.Playlists.ReplaceItems(playlist.Url, replaceItemsRequest) :> Task
          else
            let playlistAddItemsRequest =
              tracksIds |> mapSpotifyTrackId |> PlaylistAddItemsRequest

            client.Playlists.AddItems(playlist.Url, playlistAddItemsRequest) :> Task)
        |> Task.WhenAll
    }

  member _.UpdateCachedAsync (userId: int64) (tracksIds: SpotifyTrackId list) =
    task {
      _logger.LogInformation("Saving tracks ids to target playlists cache")

      let! targetPlaylists =
        _context
          .TargetPlaylists
          .AsNoTracking()
          .Where(fun p -> p.PlaylistType = PlaylistType.Target && p.UserId = userId)
          .ToListAsync()

      printfn "Saving tracks to target playlist"

      return!
        targetPlaylists
        |> Seq.map (fun playlist ->
          if playlist.Overwrite then
            _cache.SetStringAsync(
              playlist.Url,
              JsonSerializer.Serialize(tracksIds |> List.map SpotifyTrackId.value),
              DistributedCacheEntryOptions(AbsoluteExpirationRelativeToNow = TimeSpan(7, 0, 0, 0))
            )
          else
            task {
              let! value = _cache.GetStringAsync(playlist.Url)

              let newValue =
                value
                |> JsonSerializer.Deserialize<string list>
                |> List.append (tracksIds |> List.map SpotifyTrackId.value)
                |> JsonSerializer.Serialize

              return
                _cache.SetStringAsync(
                  playlist.Url,
                  newValue,
                  DistributedCacheEntryOptions(AbsoluteExpirationRelativeToNow = TimeSpan(7, 0, 0, 0))
                )
            })
        |> Task.WhenAll
    }
