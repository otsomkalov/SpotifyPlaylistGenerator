module internal Infrastructure.Cache.Redis

open Domain.Core
open Domain.Repos
open Domain.Workflows
open Infrastructure
open Microsoft.ApplicationInsights
open MusicPlatform
open StackExchange.Redis
open Infrastructure.Helpers
open otsom.fs.Core
open otsom.fs.Extensions
open System.Threading.Tasks

let private listCachedTracks telemetryClient cache =
  fun key ->
    Redis.loadList telemetryClient cache key
    |> Task.map (List.ofArray >> List.map (string >> JSON.deserialize<Track>))

let private serializeTracks tracks =
  tracks |> List.map (JSON.serialize >> RedisValue)

[<RequireQualifiedAccess>]
module UserRepo =
  let usersTracksDatabase = 1

  let listLikedTracks telemetryClient (multiplexer: IConnectionMultiplexer) (listLikedTracks: UserRepo.ListLikedTracks) userId : UserRepo.ListLikedTracks =
    let database = multiplexer.GetDatabase(usersTracksDatabase)
    let listCachedTracks = listCachedTracks telemetryClient database
    let key = userId |> UserId.value |> string

    fun () ->
      listCachedTracks key
      |> Task.bind (function
        | [] ->
          task {
            let! likedTracks = listLikedTracks ()

            do! Redis.replaceList telemetryClient database key (serializeTracks likedTracks)

            return likedTracks
          }
        | tracks -> Task.FromResult tracks)

[<RequireQualifiedAccess>]
module Playlist =
  let playlistsDatabase = 0

  let private getPlaylistsDatabase (multiplexer: IConnectionMultiplexer) =
    multiplexer.GetDatabase playlistsDatabase

  let appendTracks (telemetryClient: TelemetryClient) multiplexer =
    let prependList = Redis.prependList telemetryClient (getPlaylistsDatabase multiplexer)

    fun playlistId tracks ->
      prependList (playlistId |> PlaylistId.value) (serializeTracks tracks)

  let replaceTracks (telemetryClient: TelemetryClient) multiplexer =
    let replaceList = Redis.replaceList telemetryClient (getPlaylistsDatabase multiplexer)

    fun playlistId tracks ->
      replaceList (playlistId |> PlaylistId.value) (serializeTracks tracks)

  let listTracks telemetryClient multiplexer : Playlist.ListTracks =
    PlaylistId.value
    >> (listCachedTracks telemetryClient (getPlaylistsDatabase multiplexer))

  let countTracks telemetryClient multiplexer =
    let listLength = Redis.listLength telemetryClient (getPlaylistsDatabase multiplexer)

    PlaylistId.value
    >> listLength
