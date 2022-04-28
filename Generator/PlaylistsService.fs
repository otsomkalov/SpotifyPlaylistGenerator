module PlaylistsService

open System.Threading.Tasks

let private tryLoadPlaylist client refreshCache playlistId =
    FileService.loadIdsFromFile $"{playlistId}.json" (PlaylistService.listTracksIdsFromSpotifyPlaylist client playlistId) refreshCache

let listPlaylistsTracksIds client playlistsIds refreshCache =
    task {
        printfn "Downloading tracks ids from playlists"

        let! playlistsTracks =
            playlistsIds
            |> Seq.map (tryLoadPlaylist client refreshCache)
            |> Task.WhenAll

        return playlistsTracks |> Seq.concat
    }
