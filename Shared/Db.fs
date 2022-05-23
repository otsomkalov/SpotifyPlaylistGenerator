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
