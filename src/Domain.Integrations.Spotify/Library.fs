namespace Domain.Integrations.Spotify

open System.Collections.Generic
open System.Net
open Domain.Repos
open Domain.Workflows
open FSharp
open Microsoft.Extensions.Logging
open SpotifyAPI.Web
open otsom.fs.Extensions
open Domain.Integrations.Spotify.Helpers

[<RequireQualifiedAccess>]
module PlaylistRepo =
  let loadTracks' limit loadBatch = async {
    let! initialBatch, totalCount = loadBatch 0

    return!
      match totalCount |> Option.ofNullable with
      | Some count ->
        [ limit..limit..count ]
        |> List.map (loadBatch >> Async.map fst)
        |> Async.Sequential
        |> Async.map (List.concat >> (List.append initialBatch))
      | None -> initialBatch |> async.Return
  }

  let rec listTracks' (client: ISpotifyClient) playlistId (offset: int) = async {
    let! tracks =
      client.Playlists.GetItems(playlistId, PlaylistGetItemsRequest(Offset = offset))
      |> Async.AwaitTask

    return
      (tracks.Items
       |> Seq.choose (fun x ->
         match x.Track with
         | :? FullTrack as t -> Some t
         | _ -> None)
       |> getTracksIds,
       tracks.Total)
  }

  let listPlaylistTracks (logger: ILogger) client : PlaylistRepo.ListTracks =
    let playlistTracksLimit = 100

    fun playlistId ->
      let playlistId = playlistId |> PlaylistId.value
      let listPlaylistTracks = listTracks' client playlistId
      let loadTracks' = loadTracks' playlistTracksLimit

      task {
        try
          return! loadTracks' listPlaylistTracks

        with ApiException e when e.Response.StatusCode = HttpStatusCode.NotFound ->
          Logf.logfw logger "Playlist with id %s{PlaylistId} not found in Spotify" playlistId

          return []
      }

[<RequireQualifiedAccess>]
module TargetedPlaylistRepo =
  let private getSpotifyIds =
    fun tracksIds -> tracksIds |> List.map (fun id -> $"spotify:track:{id}") |> List<string>

  let addTracks (client: ISpotifyClient) =
    fun playlistId tracksIds ->
      client.Playlists.AddItems(playlistId, tracksIds |> getSpotifyIds |> PlaylistAddItemsRequest)
      |> Task.map ignore

  let replaceTracks (client: ISpotifyClient) =
    fun playlistId tracksIds ->
      client.Playlists.ReplaceItems(playlistId, tracksIds |> getSpotifyIds |> PlaylistReplaceItemsRequest)
      |> Task.ignore
