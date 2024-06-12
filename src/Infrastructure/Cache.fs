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

      return ()
    }

let cacheUserTracks telemetryClient cache =
  fun userId tracks ->
    let key = userId |> UserId.value |> string

    cacheTracks telemetryClient cache key tracks

let cachePlaylistTracks telemetryClient cache =
  fun playlistId tracks ->
    let key = playlistId |> PlaylistId.value |> string

    cacheTracks telemetryClient cache key tracks

let listLikedTracks telemetryClient cache userId : UserRepo.ListLikedTracks =
  fun () -> userId |> UserId.value |> string |> (listCachedTracks telemetryClient cache)

let listPlaylistTracks telemetryClient (cache: IDatabase) : PlaylistRepo.ListTracks =
  PlaylistId.value
  >> (listCachedTracks telemetryClient cache)
