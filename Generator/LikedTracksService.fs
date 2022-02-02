module LikedTracksService

open System.IO
open System.Threading.Tasks
open SpotifyAPI.Web

let rec private listLikedTracksFromSpotify (client: ISpotifyClient) (offset: int) =
    task {
        let! tracks = client.Library.GetTracks(LibraryTracksRequest(Offset = offset, Limit = 50))

        let! nextTracks =
            if tracks.Next = null then
                Seq.empty |> Task.FromResult
            else
                listLikedTracksFromSpotify client (offset + 50)

        return Seq.append nextTracks tracks.Items
    }

let private listLikedTracksIdsFromSpotify client =
    task {
        let! tracks = listLikedTracksFromSpotify client 0

        return tracks |> Seq.map (fun x -> x.Track.Id)
    }

let listLikedTracksIds client =
    if File.Exists "Liked.json" then
        FileService.readIdsFromFile "Liked.json"
    else
        task {
            let! likedTracksIds = listLikedTracksIdsFromSpotify client

            do! FileService.saveIdsToFile "Liked.json" likedTracksIds

            return likedTracksIds
        }
