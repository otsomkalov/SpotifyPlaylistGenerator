namespace Infrastructure.Workflows

open System.Collections.Generic
open System.Threading.Tasks
open Database
open Domain.Core
open Domain.Workflows
open Infrastructure.Core
open Infrastructure.Mapping
open Microsoft.EntityFrameworkCore
open System.Linq
open Infrastructure.Helpers
open Microsoft.Extensions.Caching.Distributed
open SpotifyAPI.Web
open StackExchange.Redis
open StackExchange.Redis

[<RequireQualifiedAccess>]
module UserSettings =
  let load (context: AppDbContext) : UserSettings.Load =
    let loadFromDb userId =
      context.Users
        .AsNoTracking()
        .Where(fun u -> u.Id = userId)
        .Select(fun u -> u.Settings)
        .FirstOrDefaultAsync()

    UserId.value >> loadFromDb >> Task.map UserSettings.fromDb

  let update (context: AppDbContext) : UserSettings.Update =
    fun userId settings ->
      task {
        let settings = settings |> UserSettings.toDb

        context.Users.Update(Database.Entities.User(Id = (userId |> UserId.value), Settings = settings))
        |> ignore

        let! _ = context.SaveChangesAsync()

        return ()
      }

[<RequireQualifiedAccess>]
module User =
  let rec private listLikedTracks' (client: ISpotifyClient) (offset: int) =
    async {
      let! tracks =
        client.Library.GetTracks(LibraryTracksRequest(Offset = offset, Limit = 50))
        |> Async.AwaitTask

      let! nextTracksIds =
        if Seq.isEmpty tracks.Items then
          [] |> async.Return
        else
          listLikedTracks' client (offset + 50)

      let currentTracksIds = tracks.Items |> List.ofSeq |> List.map (fun x -> x.Track.Id)

      return List.append nextTracksIds currentTracksIds
    }

  let listLikedTracks (client: ISpotifyClient) : User.ListLikedTracks = listLikedTracks' client 0

  let load (context: AppDbContext) : User.Load =
    fun userId ->
      let userId = userId |> UserId.value

      context.Users
        .AsNoTracking()
        .Include(fun x -> x.SourcePlaylists.Where(fun p -> not p.Disabled))
        .Include(fun x -> x.HistoryPlaylists.Where(fun p -> not p.Disabled))
        .Include(fun x -> x.TargetPlaylists.Where(fun p -> not p.Disabled))
        .FirstOrDefaultAsync(fun u -> u.Id = userId)
      |> Async.AwaitTask
      |> Async.map User.fromDb

[<RequireQualifiedAccess>]
module TargetPlaylist =
  let update (cache: IDatabase) (client: ISpotifyClient) : Playlist.Update =
    fun playlist tracksIds ->
      let tracksIds = tracksIds |> List.map TrackId.value
      let playlistId = playlist.Id |> WritablePlaylistId.value

      let spotifyTracksIds =
        tracksIds |> List.map (fun id -> $"spotify:track:{id}") |> List<string>

      if playlist.Overwrite then
        task {
          let replaceItemsRequest = spotifyTracksIds |> PlaylistReplaceItemsRequest

          let transaction = cache.CreateTransaction()

          let deleteTask = transaction.KeyDeleteAsync(playlistId) :> Task

          let addTask =
            transaction.ListLeftPushAsync(playlistId, (tracksIds |> List.map RedisValue |> Seq.toArray)) :> Task

          let! _ = transaction.ExecuteAsync()

          let! _ = deleteTask
          let! _ = addTask

          let! _ = client.Playlists.ReplaceItems(playlistId, replaceItemsRequest)

          ()
        }
        |> Async.AwaitTask
      else
        let playlistAddItemsRequest = spotifyTracksIds |> PlaylistAddItemsRequest

        [ cache.ListLeftPushAsync(playlistId, (tracksIds |> List.map RedisValue |> Seq.toArray)) :> Task
          client.Playlists.AddItems(playlistId, playlistAddItemsRequest) :> Task ]
        |> Task.WhenAll
        |> Async.AwaitTask
