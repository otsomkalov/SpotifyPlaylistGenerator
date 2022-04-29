module LikedTracksService

open System.Threading.Tasks
open Spotify
open SpotifyAPI.Web

let rec private listLikedTracksIdsFromSpotify' (client: ISpotifyClient) (offset: int) =
    task {
        let! tracks = client.Library.GetTracks(LibraryTracksRequest(Offset = offset, Limit = 50))

        let! nextTracksIds =
            if tracks.Next = null then
                [] |> Task.FromResult
            else
                listLikedTracksIdsFromSpotify' client (offset + 50)

        let currentTracksIds =
            tracks.Items
            |> List.ofSeq
            |> List.map (fun x -> x.Track.Id)
            |> List.map RawTrackId.create

        return List.append nextTracksIds currentTracksIds
    }

let listLikedTracksIdsFromSpotify client = listLikedTracksIdsFromSpotify' client 0
