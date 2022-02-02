module PlaylistsService

open System.Threading.Tasks

let listPlaylistsTracksIds client playlistsIds =
    task {
        let! playlistsTracks =
            playlistsIds
            |> Seq.map (PlaylistService.listTracksIdsFromSpotifyPlaylist client)
            |> Task.WhenAll

        return playlistsTracks |> Seq.concat
    }
