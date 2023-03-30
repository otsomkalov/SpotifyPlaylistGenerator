namespace Generator.Worker.Services

open System.Collections.Generic
open System.Threading.Tasks
open Database
open Infrastructure.Core
open Microsoft.Extensions.Logging
open Shared.Services
open SpotifyAPI.Web
open System.Linq
open Microsoft.EntityFrameworkCore
open StackExchange.Redis
open Infrastructure.Helpers

type TargetPlaylistService
  (
    _spotifyClientProvider: SpotifyClientProvider,
    _logger: ILogger<TargetPlaylistService>,
    _context: AppDbContext,
    _cache: IDatabase
  ) =
  member _.SaveTracksAsync (userId: int64) tracksIds =
    async {
      _logger.LogInformation("Saving tracks ids to target playlists")

      let! targetPlaylists =
        _context
          .TargetPlaylists
          .AsNoTracking()
          .Where(fun p -> p.UserId = userId)
          .ToListAsync()
          |> Async.AwaitTask

      _logger.LogInformation "Saving tracks to target playlist"

      let client = _spotifyClientProvider.Get userId

      return!
        targetPlaylists
        |> Seq.map (fun playlist ->
          let tracksIds = tracksIds |> List.map TrackId.value

          let spotifyTracksIds =
            tracksIds |> List.map (fun id -> $"spotify:track:{id}") |> List<string>

          if playlist.Overwrite then
            task{
              let replaceItemsRequest = spotifyTracksIds |> PlaylistReplaceItemsRequest

              let transaction = _cache.CreateTransaction()

              let deleteTask = transaction.KeyDeleteAsync(playlist.Url) :> Task
              let addTask = transaction.ListLeftPushAsync(playlist.Url, (tracksIds |> List.map RedisValue |> Seq.toArray)) :> Task

              let! _ = transaction.ExecuteAsync()

              let! _ = deleteTask
              let! _ = addTask

              let! _ = client.Playlists.ReplaceItems(playlist.Url, replaceItemsRequest)

              ()
            } :> Task
          else
            let playlistAddItemsRequest = spotifyTracksIds |> PlaylistAddItemsRequest

            [ _cache.ListLeftPushAsync(playlist.Url, (tracksIds |> List.map RedisValue |> Seq.toArray)) :> Task
              client.Playlists.AddItems(playlist.Url, playlistAddItemsRequest) :> Task ]
            |> Task.WhenAll)
        |> Task.WhenAll
        |> Async.AwaitTask
    }
