module Infrastructure.Cache

open System
open System.Threading.Tasks
open Domain.Core
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
    let dependency = DependencyTelemetry("Redis", key, "ListRangeAsync", key)

    using (telemetryClient.StartOperation dependency) (fun operation ->
      key
      |> RedisKey
      |> cache.ListRangeAsync
      |> Task.tap (fun v -> operation.Telemetry.Success <- true))

let private saveList (telemetryClient: TelemetryClient) (cache: IDatabase) =
  fun key (value: 'a array) ->
    let dependency = DependencyTelemetry("Redis", key, "ListLeftPushAsync", key)

    using (telemetryClient.StartOperation dependency) (fun operation ->
      cache.ListLeftPushAsync(key, value)
      |> Task.tap (fun v -> operation.Telemetry.Success <- true)
      |> Task.ignore)

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

[<RequireQualifiedAccess>]
module User =
  let listLikedTracks
    telemetryClient
    (cache: IDatabase)
    logLikedTracks
    (listLikedTracks: User.ListLikedTracks)
    userId
    : User.ListLikedTracks =
    fun () ->
      let key = userId |> UserId.value |> string
      let cacheTracks = cacheTracks telemetryClient cache key

      key
      |> (listCachedTracks telemetryClient cache)
      |> Task.bind (function
        | [] -> listLikedTracks () |> Task.bind cacheTracks
        | v -> v |> Task.FromResult)
      |> Task.tee (fun t -> logLikedTracks t.Length)

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