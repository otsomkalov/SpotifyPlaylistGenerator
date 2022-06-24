module Shared.Spotify

open System.Collections.Generic
open System.Threading.Tasks
open Microsoft.FSharp.Core
open Shared.Services
open Shared.Settings
open SpotifyAPI.Web

type RawTrackId = RawTrackId of string

module RawTrackId =
  let create id = RawTrackId id
  let value (RawTrackId id) = id

type SpotifyTrackId = SpotifyTrackId of string

module SpotifyTrackId =
  let create (RawTrackId id) = SpotifyTrackId $"spotify:track:{id}"
  let value (SpotifyTrackId str) = str

  let rawValue (SpotifyTrackId str) =
    str.Split(":") |> Array.last |> RawTrackId.create

[<Interface>]
type ISpotify =
  abstract Provider: SpotifyClientProvider

let getClient (userId: int64) (env: #ISpotify)  = env.Provider.Get userId

let getClientBySpotifyId (env: #ISpotify) (spotifyId: string) = env.Provider.Get spotifyId

let setClient (env: #ISpotify) (userId: int64) (client: ISpotifyClient) = env.Provider.SetClient(userId, client)

let setClientBySpotifyId (spotifyId: string) (client: ISpotifyClient) (env: #ISpotify) = env.Provider.SetClient(spotifyId, client)

let rec private listLikedTracksIds' (env: #ISpotify) (userId: int64) (offset: int) =
  task {
    let client = env.Provider.Get userId

    let! tracks = client.Library.GetTracks(LibraryTracksRequest(Offset = offset, Limit = 50))

    let! nextTracksIds =
      if tracks.Next = null then
        [] |> Task.FromResult
      else
        listLikedTracksIds' env userId (offset + 50)

    let currentTracksIds =
      tracks.Items
      |> List.ofSeq
      |> List.map (fun x -> x.Track.Id)
      |> List.map RawTrackId.create

    return nextTracksIds @ currentTracksIds
  }

let rec private listPlaylistTracksIds' (env: #ISpotify) (userId: int64) playlistId (offset: int) =
  task {
    let client = env.Provider.Get userId

    let! tracks = client.Playlists.GetItems(playlistId, PlaylistGetItemsRequest(Offset = offset))

    let! nextTracksIds =
      if tracks.Next = null then
        [] |> Task.FromResult
      else
        listPlaylistTracksIds' env userId playlistId (offset + 100)

    let currentTracksIds =
      tracks.Items
      |> List.ofSeq
      |> List.map (fun x -> x.Track :?> FullTrack)
      |> List.map (fun x -> x.Id)
      |> List.map RawTrackId.create

    return List.append nextTracksIds currentTracksIds
  }

let listLikedTracksIds (userId: int64) (env: #ISpotify) = listLikedTracksIds' env userId 0

let listPlaylistTracksIds (userId: int64) playlistId (env: #ISpotify) =
  listPlaylistTracksIds' env userId playlistId 0

let appendTracksToPlaylist (env: #ISpotify) (userId: int64) playlistUrl tracksIds =
  task {
    let client = env.Provider.Get userId

    let addItemsRequest =
      tracksIds
      |> List.map SpotifyTrackId.value
      |> List<string>
      |> PlaylistAddItemsRequest

    let! _ = client.Playlists.AddItems(playlistUrl, addItemsRequest)

    ()
  }

let replaceTracksInPlaylist (env: #ISpotify) (userId: int64) playlistUrl tracksIds =
  task {
    let replaceItemsRequest =
      tracksIds
      |> List.map SpotifyTrackId.value
      |> List<string>
      |> PlaylistReplaceItemsRequest

    let client = env.Provider.Get userId

    let! _ = client.Playlists.ReplaceItems(playlistUrl, replaceItemsRequest)

    ()
  }