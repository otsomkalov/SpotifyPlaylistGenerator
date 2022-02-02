module PlaylistService

open System.IO
open System.Threading.Tasks
open SpotifyAPI.Web

let rec private listTracksFromSpotifyPlaylist (client: ISpotifyClient) playlistId (offset: int) =
    task {
        let! tracks = client.Playlists.GetItems(playlistId, PlaylistGetItemsRequest(Offset = offset))

        let! nextTracks =
            if tracks.Next = null then
                Seq.empty |> Task.FromResult
            else
                listTracksFromSpotifyPlaylist client playlistId (offset + 100)

        return Seq.append nextTracks tracks.Items
    }

let listTracksIdsFromSpotifyPlaylist client playlistId =
    task {
        let! tracks = listTracksFromSpotifyPlaylist client playlistId 0

        return
            tracks
            |> Seq.map (fun x -> x.Track)
            |> Seq.map (fun x -> x :?> FullTrack)
            |> Seq.map (fun x -> x.Id)
    }

let listTracksIds fileName listTracksIdsFunc =
    if File.Exists fileName then
        printfn $"Reading tracks ids from {fileName}"

        FileService.readIdsFromFile fileName
    else
        task {
            printfn "Downloading tracks ids from Spotify"

            let! tracksIds = listTracksIdsFunc

            printfn "Saving tracks ids to file..."

            do! FileService.saveIdsToFile fileName tracksIds

            return tracksIds
        }