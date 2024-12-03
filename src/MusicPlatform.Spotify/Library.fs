namespace MusicPlatform.Spotify

open System.Net
open FSharp
open Microsoft.Extensions.Logging
open Microsoft.FSharp.Control
open MusicPlatform
open SpotifyAPI.Web
open MusicPlatform.Spotify.Helpers
open otsom.fs.Extensions

[<RequireQualifiedAccess>]
module Playlist =
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

  let listTracks (logger: ILogger) client : Playlist.ListTracks =
    let playlistTracksLimit = 100

    fun (PlaylistId playlistId) ->
      let listPlaylistTracks = listTracks' client playlistId
      let loadTracks' = loadTracks' playlistTracksLimit

      task {
        try
          return! loadTracks' listPlaylistTracks

        with ApiException e when e.Response.StatusCode = HttpStatusCode.NotFound ->
          Logf.logfw logger "Playlist with id %s{PlaylistId} not found in Spotify" playlistId

          return []
      }
