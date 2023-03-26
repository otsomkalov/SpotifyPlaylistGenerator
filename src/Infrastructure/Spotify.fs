module Infrastructure.Spotify

open System.Net
open System.Threading.Tasks
open Domain.Workflows
open Microsoft.Extensions.Logging
open SpotifyAPI.Web

let rec private listTracks' (client: ISpotifyClient) playlistId (offset: int) =
  task {
    let! tracks = client.Playlists.GetItems(playlistId, PlaylistGetItemsRequest(Offset = offset))

    let! nextTracksIds =
      if tracks.Next = null then
        [] |> Task.FromResult
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
    task {
      try
        return! listTracks' client playlistId 0
      with
      | :? APIException as e when e.Response.StatusCode = HttpStatusCode.NotFound ->
        logger.LogInformation("Playlist with id {PlaylistId} not found in Spotify", playlistId)

        return []
    }
