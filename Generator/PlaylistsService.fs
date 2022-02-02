module PlaylistsService

open System.Threading.Tasks

let listPlaylistsTracksIds client playlistsIds =
    task {
        printfn "Downloading tracks ids from playlists"

        let! playlistsTracks =
            playlistsIds
            |> Seq.map (PlaylistService.listTracksIdsFromSpotifyPlaylist client)
            |> Task.WhenAll

        return playlistsTracks |> Seq.concat
    }
