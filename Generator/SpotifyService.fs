module SpotifyService

open System.Collections.Generic
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

        let! _ = client.Playlists.ReplaceItems(playlistId, replaceItemsRequest)

        return ()
    }

let saveTracksToHistoryPlaylist (client: ISpotifyClient) historyPlaylistId (tracksIds: SpotifyTrackId seq) =
    task {
        let addItemsRequest =
            tracksIds
            |> Seq.map SpotifyTrackId.value
            |> List<string>
            |> PlaylistAddItemsRequest

        printfn "Saving tracks to history playlist"

        let! _ = client.Playlists.AddItems(historyPlaylistId, addItemsRequest)

        return ()
    }
