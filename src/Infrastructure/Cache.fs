module Infrastructure.Cache

open System
open System.Threading.Tasks
open Domain.Core
open Domain.Workflows
open Infrastructure.Core
open StackExchange.Redis
open Domain.Extensions
open Infrastructure.Helpers

let private listCachedTracks (cache: IDatabase) =
  fun key -> key |> RedisKey |> cache.ListRangeAsync |> Task.map (List.ofArray >> List.map (string >> TrackId))

let private cacheTracks (cache: IDatabase) =
  fun key tracks ->
    task {
      let values = tracks |> List.map (TrackId.value >> RedisValue) |> Array.ofSeq

      let! _ = cache.ListLeftPushAsync(key, values)
      let! _ = cache.KeyExpireAsync(key, TimeSpan.FromDays(7))

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
      |> Task.taskMap (function
        | [] -> listLikedTracks () |> Task.taskMap cacheTracks
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
      |> Task.taskMap (function
        | [] -> (listTracks id) |> Task.taskMap cacheTracks
        | v -> v |> Task.FromResult)
