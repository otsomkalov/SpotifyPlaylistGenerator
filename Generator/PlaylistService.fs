module PlaylistService

open System.IO
open System.Threading.Tasks
open Spotify
open SpotifyAPI.Web

let rec private listTracksIdsFromSpotifyPlaylist' (client: ISpotifyClient) playlistId (offset: int) =
    task {
        let! tracks = client.Playlists.GetItems(playlistId, PlaylistGetItemsRequest(Offset = offset))

        let! nextTracksIds =
            if tracks.Next = null then
                Seq.empty |> Task.FromResult
            else
                listTracksIdsFromSpotifyPlaylist' client playlistId (offset + 100)

        let currentTracksIds =
            tracks.Items
            |> Seq.map (fun x -> x.Track :?> FullTrack)
            |> Seq.map (fun x -> x.Id)
            |> Seq.map RawTrackId.create

        return Seq.append nextTracksIds currentTracksIds
    }

let listTracksIdsFromSpotifyPlaylist client playlistId =
    listTracksIdsFromSpotifyPlaylist' client playlistId 0

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
