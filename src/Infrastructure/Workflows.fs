namespace Infrastructure.Workflows

open Database
open Domain.Core
open Domain.Workflows
open Infrastructure.Core
open Infrastructure.Mapping
open Microsoft.EntityFrameworkCore
open System.Linq
open Infrastructure.Helpers
open SpotifyAPI.Web

[<RequireQualifiedAccess>]
module UserSettings =
  let load (context: AppDbContext) : UserSettings.Load =
    let loadFromDb userId =
      context
        .Users
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

      context
        .Users
        .AsNoTracking()
        .Include(fun x -> x.SourcePlaylists.Where(fun p -> not p.Disabled))
        .Include(fun x -> x.HistoryPlaylists.Where(fun p -> not p.Disabled))
        .Include(fun x -> x.TargetPlaylists.Where(fun p -> not p.Disabled))
        .FirstOrDefaultAsync(fun u -> u.Id = userId)
        |> Async.AwaitTask
        |> Async.map User.fromDb
