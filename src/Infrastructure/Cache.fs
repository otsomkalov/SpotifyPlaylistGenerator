module Infrastructure.Cache

open System
open System.Threading.Tasks
open Domain.Core
open Domain.Repos
open Domain.Workflows
open Microsoft.ApplicationInsights
open Microsoft.ApplicationInsights.DataContracts
open StackExchange.Redis
open Infrastructure.Helpers
open otsom.fs.Extensions
open otsom.fs.Telegram.Bot.Core

[<Literal>]
let playlistsDatabase = 0

[<Literal>]
let likedTracksDatabase = 1

let private loadList (telemetryClient: TelemetryClient) (cache: IDatabase) =
  fun key ->
    task {
      let dependency = DependencyTelemetry("Redis", key, "ListRangeAsync", key)

      use operation = telemetryClient.StartOperation dependency

      let! values = key |> RedisKey |> cache.ListRangeAsync

      operation.Telemetry.Success <- values.Length > 0

      return values
    }

let private saveList (telemetryClient: TelemetryClient) (cache: IDatabase) =
  fun key (value: 'a array) ->
    task{
      let dependency = DependencyTelemetry("Redis", key, "ListLeftPushAsync", key)

      use operation = telemetryClient.StartOperation dependency

      let! _ = cache.ListLeftPushAsync(key, value)

      operation.Telemetry.Success <- true

      return ()
    }

let private listCachedTracks telemetryClient cache =
  fun key ->
    loadList telemetryClient cache key
    |> Task.map (List.ofArray >> List.map (string >> JSON.deserialize<Track>))

let private cacheTracks telemetryClient cache =
  fun key tracks ->
    task {
      let values = tracks |> List.map (JSON.serialize >> RedisValue) |> Array.ofSeq

      do! saveList telemetryClient cache key values
      let! _ = cache.KeyExpireAsync(key, TimeSpan.FromDays(1))

      return tracks
    }

let cacheUserTracks telemetryClient cache =
  fun userId tracks ->
    let key = userId |> UserId.value |> string

    cacheTracks telemetryClient cache key tracks |> Task.ignore

let listLikedTracks telemetryClient cache userId : UserRepo.ListLikedTracks =
  fun () ->
    let key = userId |> UserId.value |> string

    key |> (listCachedTracks telemetryClient cache)

[<RequireQualifiedAccess>]
module Playlist =
  let listTracks telemetryClient (cache: IDatabase) (listTracks: Playlist.ListTracks) : Playlist.ListTracks =
    fun id ->
      let key = id |> ReadablePlaylistId.value |> PlaylistId.value
      let cacheTracks = cacheTracks telemetryClient cache key

      key
      |> (listCachedTracks telemetryClient cache)
      |> Task.bind (function
        | [] -> (listTracks id) |> Task.bind cacheTracks
        | v -> v |> Task.FromResult)
