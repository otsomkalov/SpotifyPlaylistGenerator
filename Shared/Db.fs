module Shared.Db

open Database
open Database.Entities
open Microsoft.EntityFrameworkCore
open System.Linq

[<Interface>]
type IDb =
  abstract Db: AppDbContext

let getUser (env: #IDb) userId =
  env
    .Db
    .Users
    .AsNoTracking()
    .FirstOrDefaultAsync(fun u -> u.Id = userId)

let private listPlaylistsUrls (env: #IDb) userId playlistType =
  env
    .Db
    .Playlists
    .AsNoTracking()
    .Where(fun p -> p.UserId = userId && p.PlaylistType = playlistType)
    .Select(fun p -> p.Url)
    .ToListAsync()

let private getPlaylistUrl (env: #IDb) userId playlistType =
  env
    .Db
    .Playlists
    .AsNoTracking()
    .Where(fun p -> p.UserId = userId && p.PlaylistType = playlistType)
    .Select(fun p -> p.Url)
    .FirstOrDefaultAsync()

let listUserHistoryPlaylistsUrls (env: #IDb) userId =
  listPlaylistsUrls env userId PlaylistType.History

let listUserSourcePlaylistsUrls (env: #IDb) userId =
  listPlaylistsUrls env userId PlaylistType.Source

let getTargetHistoryPlaylistUrl (env: #IDb) userId =
  getPlaylistUrl env userId PlaylistType.TargetHistory

let getTargetPlaylistUrl (env: #IDb) userId =
  getPlaylistUrl env userId PlaylistType.Target

let getTargetHistoryPlaylist (env: #IDb) userId =
  env
    .Db
    .Playlists
    .AsNoTracking()
    .FirstOrDefaultAsync(fun p ->
      p.UserId = userId
      && p.PlaylistType = PlaylistType.TargetHistory)

let userExists (env: #IDb) (userId: int64) =
  env
    .Db
    .Users
    .AsNoTracking()
    .AnyAsync(fun u -> u.Id = userId)

let createUser (env: #IDb) (userId: int64) =
  task {
    let! _ = User(Id = userId) |> env.Db.AddAsync

    let! _ = env.Db.SaveChangesAsync()

    return ()
  }

let updateUser (env: #IDb) user =
  task {
    env.Db.Update user |> ignore

    let! _ = env.Db.SaveChangesAsync()

    return ()
  }

let updatePlaylist (env: #IDb) playlist =
  task {
    env.Db.Update playlist |> ignore

    let! _ = env.Db.SaveChangesAsync()

    return ()
  }

let getUserPlaylistsTypes (env: #IDb) userId =
  env
    .Db
    .Playlists
    .AsNoTracking()
    .Where(fun p -> p.UserId = userId)
    .Select(fun p -> p.PlaylistType)
    .ToListAsync()

let createPlaylist (env: #IDb) url userId playlistType =
  task {
    let! _ =
      Playlist(Url = url, UserId = userId, PlaylistType = playlistType)
      |> env.Db.Playlists.AddAsync

    let! _ = env.Db.SaveChangesAsync()

    return ()
  }

let getTargetPlaylist (env: #IDb) userId =
  env
    .Db
    .Playlists
    .AsNoTracking()
    .FirstOrDefaultAsync(fun p ->
      p.UserId = userId
      && p.PlaylistType = PlaylistType.Target)
