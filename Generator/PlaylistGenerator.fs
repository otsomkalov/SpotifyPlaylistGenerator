module PlaylistGenerator

open System.Threading.Tasks

let generatePlaylist listLikedTracksIds listHistoryTracksIds listPlaylistsTracksIds importTracksToSpotify updateHistoryTracksIdsFile =
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

        let spotifyTracksIdsToImport =
            tracksIdsToImport
            |> Seq.map SpotifyService.idToSpotifyId
            |> List.ofSeq

        let! importTracksResult = importTracksToSpotify spotifyTracksIdsToImport

        return!
            match importTracksResult with
            | Error e -> Error e |> Task.FromResult
            | Ok _ ->
                task {
                    let newHistoryTracks =
                        Seq.append historyTracksIds tracksIdsToImport

                    do! updateHistoryTracksIdsFile newHistoryTracks

                    return Ok()
                }
    }
