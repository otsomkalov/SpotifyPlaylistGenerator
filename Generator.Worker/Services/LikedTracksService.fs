module Generator.Worker.Services.LikedTracksService

open Shared

let listIdsAsync env (userId: int64) refreshCache =
  task {
    let! tracksIds = TracksIdsService.readOrDownloadAsync env "LikedTracks.json" (Spotify.listLikedTracksIds userId) refreshCache

    Log.info env ("User with Telegram id {TelegramId} has {LikedTracksIdsCount} liked tracks", [ userId; tracksIds.Length ])

    return tracksIds
  }
