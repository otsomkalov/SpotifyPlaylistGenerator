namespace MusicPlatform.Spotify

open System.Net
open FSharp
open Microsoft.Extensions.Logging
open Microsoft.FSharp.Control
open MusicPlatform
open SpotifyAPI.Web
open MusicPlatform.Spotify.Helpers
open otsom.fs.Extensions
open System.Collections.Generic

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

  let private getSpotifyIds =
    fun (tracks: Track list) ->
      tracks
      |> List.map (_.Id)
      |> List.map (fun (TrackId id) -> $"spotify:track:{id}")
      |> List<string>

  let addTracks (client: ISpotifyClient) : Playlist.AddTracks =
    fun (PlaylistId playlistId) tracks ->
      client.Playlists.AddItems(playlistId, tracks |> getSpotifyIds |> PlaylistAddItemsRequest)
      &|> ignore

  let replaceTracks (client: ISpotifyClient) : Playlist.ReplaceTracks =
    fun (PlaylistId playlistId) tracks ->
      client.Playlists.ReplaceItems(playlistId, tracks |> getSpotifyIds |> PlaylistReplaceItemsRequest)
      &|> ignore

  let load (client: ISpotifyClient) : Playlist.Load =
    fun (PlaylistId playlistId) -> task {
      try
        let! playlist = playlistId |> client.Playlists.Get

        let! currentUser = client.UserProfile.Current()

        let playlist =
          if playlist.Owner.Id = currentUser.Id then
            Writable(
              { Id = playlist.Id |> PlaylistId
                Name = playlist.Name }
            )
          else
            Readable(
              { Id = playlist.Id |> PlaylistId
                Name = playlist.Name }
            )

        return playlist |> Ok
      with ApiException e when e.Response.StatusCode = HttpStatusCode.NotFound ->
        return Playlist.LoadError.NotFound |> Error
    }

[<RequireQualifiedAccess>]
module User =
  let rec private listLikedTracks' (client: ISpotifyClient) (offset: int) = async {
    let! tracks =
      client.Library.GetTracks(LibraryTracksRequest(Offset = offset, Limit = 50))
      |> Async.AwaitTask

    return (tracks.Items |> Seq.map _.Track |> getTracksIds, tracks.Total)
  }

  let listLikedTracks (client: ISpotifyClient) : User.ListLikedTracks =
    let likedTacksLimit = 50

    let listLikedTracks' = listLikedTracks' client
    let loadTracks' = Playlist.loadTracks' likedTacksLimit

    fun () -> loadTracks' listLikedTracks' |> Async.StartAsTask
