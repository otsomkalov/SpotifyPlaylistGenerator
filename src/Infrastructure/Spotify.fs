module Infrastructure.Spotify

open System.Net
open Domain.Workflows
open Infrastructure.Core
open Microsoft.Extensions.Logging
open SpotifyAPI.Web
open Infrastructure.Helpers
open Infrastructure.Helpers.Spotify

let rec private listTracks' (client: ISpotifyClient) playlistId (offset: int) =
  async {
    let! tracks =
      client.Playlists.GetItems(playlistId, PlaylistGetItemsRequest(Offset = offset))
      |> Async.AwaitTask

    let! nextTracksIds =
      if tracks.Next = null then
        [] |> async.Return
      else
        listTracks' client playlistId (offset + 100)

    let currentTracksIds =
      tracks.Items
      |> Seq.filter (fun t -> isNull t.Track |> not)
      |> Seq.map (fun x -> x.Track :?> FullTrack)
      |> Seq.map (fun x -> x.Id)
      |> Seq.toList

    return List.append nextTracksIds currentTracksIds
  }

let listTracks (logger: ILogger) client : Playlist.ListTracks =
  fun playlistId ->
    async {
      try
        let playlistId = playlistId |> ReadablePlaylistId.value

        return! listTracks' client playlistId 0
      with ApiException e when e.Response.StatusCode = HttpStatusCode.NotFound ->
        logger.LogInformation("Playlist with id {PlaylistId} not found in Spotify", playlistId)

        return []
    }
