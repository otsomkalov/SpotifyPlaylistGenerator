module Generator.Worker.Services.HistoryPlaylistsService

open Generator.Worker
open Shared

let listTracksIdsAsync env userId refreshCache =
  task {
    let! historyPlaylistsUrls = Db.listUserHistoryPlaylistsUrls env userId

    let! tracksIds = PlaylistService.listTracksIdsAsync env userId historyPlaylistsUrls refreshCache

    Log.infoWithArgs
      env
      "User with Telegram id {TelegramId} has {HistoryPlaylistsTracksCount} tracks in history playlists"
      userId
      tracksIds.Length

    return tracksIds
  }

let updateAsync env (userId: int64) tracksIds =
  task {
    Log.info env "Adding new tracks to history playlist"

    let! targetHistoryPlaylistUrl = Db.getTargetHistoryPlaylistUrl env userId

    do! Spotify.appendTracksToPlaylist env userId targetHistoryPlaylistUrl tracksIds

    return ()
  }

let updateCachedAsync env userId tracksIds =
  task {
    Log.info env "Updating history playlist cache file"

    let! targetHistoryPlaylistId = Db.getTargetHistoryPlaylistUrl env userId

    return! FileService.saveIdsAsync $"{targetHistoryPlaylistId}.json" tracksIds
  }
