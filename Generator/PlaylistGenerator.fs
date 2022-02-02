module PlaylistGenerator

let generatePlaylist listLikedTracksIds listHistoryTracksIds listPlaylistsTracksIds saveTracks =
    task {
        let! likedTracksIds = listLikedTracksIds
        let! historyTracksIds = listHistoryTracksIds
        let! playlistsTracksIds = listPlaylistsTracksIds

        let tracksIdsToExclude =
            Seq.append likedTracksIds historyTracksIds
            |> Seq.distinct

        let tracksIdsToImport =
            playlistsTracksIds
            |> Seq.except tracksIdsToExclude
            |> Seq.shuffle
            |> Seq.take 100
            |> Seq.map SpotifyService.idToSpotifyId

        return! saveTracks tracksIdsToImport
    }
