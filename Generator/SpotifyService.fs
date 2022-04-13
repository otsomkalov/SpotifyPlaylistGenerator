module SpotifyService

open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open Spotify
open SpotifyAPI.Web

let saveTracksToTargetPlaylist (client: ISpotifyClient) playlistId (tracksIds: SpotifyTrackId seq) =
    task {
        let replaceItemsRequest =
            tracksIds
            |> Seq.map SpotifyTrackId.value
            |> List<string>
            |> PlaylistReplaceItemsRequest

        printfn "Saving tracks to target playlist"

        let! replaceItemsResult = client.Playlists.ReplaceItems(playlistId, replaceItemsRequest)

        return if replaceItemsResult then Some(tracksIds) else None
    }

let saveTracksToHistoryPlaylist (client: ISpotifyClient) historyPlaylistId (tracksIds: SpotifyTrackId seq) =
    task {
        let addItemsRequest =
            tracksIds
            |> Seq.map SpotifyTrackId.value
            |> List<string>
            |> PlaylistAddItemsRequest

        printfn "Saving tracks to history playlist"

        return!
            try
                task {
                    let! _ = client.Playlists.AddItems(historyPlaylistId, addItemsRequest)

                    return Some(tracksIds)
                }
            with
            | _ ->
                None |> Task.FromResult
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