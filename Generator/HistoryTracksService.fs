module HistoryTracksService

open System.IO

let listHistoryTracksIds client historyPlaylistId =
    if File.Exists "History.json" then
        FileService.readIdsFromFile "History.json"
    else
        task {
            let! historyTracksIds = PlaylistService.listTracksIdsFromSpotifyPlaylist client historyPlaylistId

            do! FileService.saveIdsToFile "History.json" historyTracksIds

            return historyTracksIds
        }
