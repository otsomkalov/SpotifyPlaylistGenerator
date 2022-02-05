module LikedTracksService

open System.Threading.Tasks
open SpotifyAPI.Web

let rec private listLikedTracksIdsFromSpotify' (client: ISpotifyClient) (offset: int) =
    task {
        let! tracks = client.Library.GetTracks(LibraryTracksRequest(Offset = offset, Limit = 50))

        let! nextTracksIds =
            if tracks.Next = null then
                Seq.empty |> Task.FromResult
            else
                listLikedTracksIdsFromSpotify' client (offset + 50)

        let currentTracksIds =
            tracks.Items
            |> Seq.map (fun x -> x.Track.Id)

        return Seq.append nextTracksIds currentTracksIds
    }

let listLikedTracksIdsFromSpotify client =
    listLikedTracksIdsFromSpotify' client 0
