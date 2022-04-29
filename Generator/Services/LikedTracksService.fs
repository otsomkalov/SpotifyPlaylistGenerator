namespace Generator.Services

open System.Threading.Tasks
open Generator
open SpotifyAPI.Web

type LikedTracksService(_client: ISpotifyClient, _idsService: TracksIdsService) =
    let rec downloadIdsAsync (offset: int) =
        task {
            let! tracks = _client.Library.GetTracks(LibraryTracksRequest(Offset = offset, Limit = 50))

            let! nextTracksIds =
                if tracks.Next = null then
                    [] |> Task.FromResult
                else
                    downloadIdsAsync (offset + 50)

            let currentTracksIds =
                tracks.Items
                |> List.ofSeq
                |> List.map (fun x -> x.Track.Id)
                |> List.map RawTrackId.create

            return List.append nextTracksIds currentTracksIds
        }

    member _.listIdsAsync =
        _idsService.readOrDownloadAsync "LikedTracks.json" (downloadIdsAsync 0)
