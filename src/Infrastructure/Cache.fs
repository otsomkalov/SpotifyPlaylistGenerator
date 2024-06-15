module internal Infrastructure.Cache

open Domain.Core
open Domain.Repos
open Domain.Workflows
open Microsoft.ApplicationInsights
open StackExchange.Redis
open Infrastructure.Helpers
open otsom.fs.Core
open otsom.fs.Extensions
open otsom.fs.Telegram.Bot.Core

let private listCachedTracks telemetryClient cache =
  fun key ->
    Redis.loadList telemetryClient cache key
    |> Task.map (List.ofArray >> List.map (string >> JSON.deserialize<Track>))

let private serializeTracks tracks =
  tracks |> List.map (JSON.serialize >> RedisValue)

[<RequireQualifiedAccess>]
module User =
  let usersTracksDatabase = 1

  let private getUsersTracksDatabase (multiplexer: IConnectionMultiplexer) =
    multiplexer.GetDatabase usersTracksDatabase

  let listLikedTracks telemetryClient multiplexer userId =
    let listCachedTracks = listCachedTracks telemetryClient (getUsersTracksDatabase multiplexer)

    fun () ->
      userId |> UserId.value |> string |> listCachedTracks

  let cacheLikedTracks telemetryClient multiplexer =
    fun userId tracks ->
      let key = userId |> UserId.value |> string

      Redis.replaceList telemetryClient (getUsersTracksDatabase multiplexer) key (serializeTracks tracks)

[<RequireQualifiedAccess>]
module Playlist =
  let playlistsDatabase = 0

  let private getPlaylistsDatabase (multiplexer: IConnectionMultiplexer) =
    multiplexer.GetDatabase playlistsDatabase

  let appendTracks (telemetryClient: TelemetryClient) multiplexer =
    let prependList = Redis.prependList telemetryClient (getPlaylistsDatabase multiplexer)

    fun playlistId tracks ->
      prependList playlistId (serializeTracks tracks)

  let replaceTracks (telemetryClient: TelemetryClient) multiplexer =
    let replaceList = Redis.replaceList telemetryClient (getPlaylistsDatabase multiplexer)

    fun playlistId tracks ->
      replaceList playlistId (serializeTracks tracks)

  let listTracks telemetryClient multiplexer : PlaylistRepo.ListTracks =
    PlaylistId.value
    >> (listCachedTracks telemetryClient (getPlaylistsDatabase multiplexer))

  let countTracks telemetryClient multiplexer =
    let listLength = Redis.listLength telemetryClient (getPlaylistsDatabase multiplexer)

    PlaylistId.value
    >> listLength
