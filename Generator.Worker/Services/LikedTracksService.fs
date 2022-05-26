module Generator.Worker.Services.LikedTracksService

open Generator.Worker
open Shared

let listIdsAsync env (userId: int64) refreshCache =
  task {
    let! tracksIds = TracksIdsService.readOrDownloadAsync env "LikedTracks.json" (Spotify.listLikedTracksIds userId) refreshCache

    Log.infoWithArgs env "User with Telegram id {TelegramId} has {LikedTracksIdsCount} liked tracks" userId tracksIds.Length

    return tracksIds
  }
