module Infrastructure.Cache

open System
open System.Threading.Tasks
open Domain.Core
open Domain.Workflows
open Infrastructure.Core
open StackExchange.Redis
open Infrastructure.Helpers
open otsom.FSharp.Extensions

[<Literal>]
let playlistsDatabase = 0
[<Literal>]
let likedTracksDatabase = 1
[<Literal>]
let tokensDatabase = 2
[<Literal>]
let authDatabase = 3

let private listCachedTracks (cache: IDatabase) =
  fun key -> key |> RedisKey |> cache.ListRangeAsync |> Task.map (List.ofArray >> List.map (string >> TrackId))

let private cacheTracks (cache: IDatabase) =
  fun key tracks ->
    task {
      let values = tracks |> List.map (TrackId.value >> RedisValue) |> Array.ofSeq

      let! _ = cache.ListLeftPushAsync(key, values)
      let! _ = cache.KeyExpireAsync(key, TimeSpan.FromDays(1))

      return tracks
    }

[<RequireQualifiedAccess>]
module User =
  let listLikedTracks
    (cache: IDatabase)
    logLikedTracks
    (listLikedTracks: User.ListLikedTracks)
    userId
    : User.ListLikedTracks =
    fun () ->
      let key = userId |> UserId.value |> string
      let cacheTracks = cacheTracks cache key

      key
      |> (listCachedTracks cache)
      |> Task.bind (function
        | [] -> listLikedTracks () |> Task.bind cacheTracks
        | v -> v |> Task.FromResult)
      |> Task.tee (fun t -> logLikedTracks t.Length)

[<RequireQualifiedAccess>]
module Playlist =
  let listTracks (cache: IDatabase) (listTracks: Playlist.ListTracks) : Playlist.ListTracks =
    fun id ->
      let key = id |> ReadablePlaylistId.value |> PlaylistId.value
      let cacheTracks = cacheTracks cache key

      key
      |> (listCachedTracks cache)
      |> Task.bind (function
        | [] -> (listTracks id) |> Task.bind cacheTracks
        | v -> v |> Task.FromResult)
