module PlaylistService

open System.Threading.Tasks
open Spotify
open SpotifyAPI.Web

let rec private listTracksIdsFromSpotifyPlaylist' (client: ISpotifyClient) playlistId (offset: int) =
    task {
        let! tracks = client.Playlists.GetItems(playlistId, PlaylistGetItemsRequest(Offset = offset))

        let! nextTracksIds =
            if tracks.Next = null then
                [] |> Task.FromResult
            else
                listTracksIdsFromSpotifyPlaylist' client playlistId (offset + 100)

        let currentTracksIds =
            tracks.Items
            |> List.ofSeq
            |> List.map (fun x -> x.Track :?> FullTrack)
            |> List.map (fun x -> x.Id)
            |> List.map RawTrackId.create

        return List.append nextTracksIds currentTracksIds
    }

let listTracksIdsFromSpotifyPlaylist client playlistId =
    listTracksIdsFromSpotifyPlaylist' client playlistId 0
