namespace Generator.Worker.Services

open System.Threading.Tasks
open Generator.Worker.Domain
open Microsoft.Extensions.Logging
open SpotifyAPI.Web
open Shared.Services

type PlaylistService(_spotifyClientProvider: SpotifyClientProvider, _idsService: TracksIdsService, _logger: ILogger<PlaylistService>) =
  let rec downloadTracksIdsAsync' (userId: int64) playlistId (offset: int) =
    fun () ->
      task {
        let client = _spotifyClientProvider.Get userId

        try
          let! tracks = client.Playlists.GetItems(playlistId, PlaylistGetItemsRequest(Offset = offset))

          let! nextTracksIds =
            if tracks.Next = null then
              [] |> Task.FromResult
            else
              downloadTracksIdsAsync' userId playlistId (offset + 100) ()

          let currentTracksIds =
            tracks.Items
            |> List.ofSeq
            |> List.filter (fun t -> isNull t.Track |> not)
            |> List.map (fun x -> x.Track :?> FullTrack)
            |> List.map (fun x -> x.Id)
            |> List.map RawTrackId.create

          return List.append nextTracksIds currentTracksIds
        with _ ->
          _logger.LogInformation("Playlist with id {PlaylistId} not found in Spotify", playlistId)
          return []
      }

  let downloadTracksIdsAsync userId playlistId =
    downloadTracksIdsAsync' userId playlistId 0

  let readOrDownloadTracksIdsAsync userId refreshCache playlistId =
    _idsService.ReadOrDownloadAsync playlistId (downloadTracksIdsAsync userId playlistId) refreshCache

  member this.ListTracksIdsAsync userId playlistsIds refreshCache =
    task {
      let! playlistsTracks =
        playlistsIds
        |> Seq.map (readOrDownloadTracksIdsAsync userId refreshCache)
        |> Task.WhenAll

      return playlistsTracks |> List.concat |> List.distinct
    }
