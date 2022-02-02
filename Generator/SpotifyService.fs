module SpotifyService

open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open SpotifyAPI.Web

let saveTracksToTargetPlaylist (client: ISpotifyClient) playlistId (tracksIds: string seq) =
    task {
        let replaceItemsRequest =
            tracksIds
            |> List<string>
            |> PlaylistReplaceItemsRequest

        printfn "Saving tracks to target playlist"

        let! replaceItemsResult = client.Playlists.ReplaceItems(playlistId, replaceItemsRequest)

        return
            if replaceItemsResult then
                Ok(tracksIds)
            else
                Error("Error during saving tracks to target playlist")
    }

let saveTracksToHistoryPlaylist (client: ISpotifyClient) historyPlaylistId (tracksIds: string seq) =
    task {
        let addItemsRequest =
            tracksIds
            |> List<string>
            |> PlaylistAddItemsRequest

        printfn "Saving tracks to history playlist"

        return!
            try
                task {
                    let! _ = client.Playlists.AddItems(historyPlaylistId, addItemsRequest)

                    return Ok()
                }
            with
            | _ ->
                Error("Error during saving tracks to history playlist")
                |> Task.FromResult
    }

let listTracksIdsFromSpotify listTracksIdsFromSpotifyFunc fileName =
    if File.Exists fileName then
        printfn $"Reading tracks ids from {fileName}"

        FileService.readIdsFromFile fileName
    else
        task {
            printfn "Downloading tracks ids from Spotify"

            let! tracksIds = listTracksIdsFromSpotifyFunc

            printfn "Saving tracks ids to file..."

            do! FileService.saveIdsToFile fileName tracksIds

            return tracksIds
        }

let idToSpotifyId id = $"spotify:track:{id}"
