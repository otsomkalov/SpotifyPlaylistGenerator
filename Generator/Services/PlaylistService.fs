namespace Generator.Services

open System.Threading.Tasks
open Generator
open SpotifyAPI.Web

type PlaylistService(_client: ISpotifyClient, _idsService: TracksIdsService) =
    let rec downloadTracksIdsAsync' playlistId (offset: int) =
        task {
            let! tracks = _client.Playlists.GetItems(playlistId, PlaylistGetItemsRequest(Offset = offset))

            let! nextTracksIds =
                if tracks.Next = null then
                    [] |> Task.FromResult
                else
                    downloadTracksIdsAsync' playlistId (offset + 100)

            let currentTracksIds =
                tracks.Items
                |> List.ofSeq
                |> List.map (fun x -> x.Track :?> FullTrack)
                |> List.map (fun x -> x.Id)
                |> List.map RawTrackId.create

            return List.append nextTracksIds currentTracksIds
        }

    let downloadTracksIdsAsync playlistId =
        downloadTracksIdsAsync' playlistId 0

    let readOrDownloadTracksIdsAsync playlistId =
        _idsService.readOrDownloadAsync $"{playlistId}.json" (downloadTracksIdsAsync playlistId)

    member _.listTracksIdsAsync playlistsIds =
        task {
            let! playlistsTracks =
                playlistsIds
                |> Seq.map readOrDownloadTracksIdsAsync
                |> Task.WhenAll

            return playlistsTracks |> List.concat
        }
