module SpotifyService

open System.Collections.Generic
open System.Threading.Tasks
open SpotifyAPI.Web

let saveTracksToTargetPlaylist (client: ISpotifyClient) playlistId (tracksIds: string seq) =
    task {
        let replaceItemsRequest =
            tracksIds
            |> List<string>
            |> PlaylistReplaceItemsRequest

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

let idToSpotifyId id = $"spotify:track:{id}"
