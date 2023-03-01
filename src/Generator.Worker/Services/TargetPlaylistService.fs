namespace Generator.Worker.Services

open System.Collections.Generic
open System.Threading.Tasks
open Database
open Database.Entities
open Generator.Worker.Domain
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
    _context: AppDbContext
  ) =
  member _.SaveTracksAsync (userId: int64) tracksIds =
    task {
      _logger.LogInformation("Saving tracks ids to target playlist")

      printfn "Saving tracks to target playlists"

      let client =
        _spotifyClientProvider.Get userId

      let! targetPlaylists =
        _context
          .TargetPlaylists
          .Where(fun x ->
            x.UserId = userId)
          .ToListAsync()

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
