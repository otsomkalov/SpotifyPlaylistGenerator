module Generator.Worker.Services.TargetPlaylistService

open Shared
open Generator.Worker

let saveTracksAsync env (userId: int64) tracksIds =
  task {
    Log.info env "Saving tracks ids to target playlist"

    let! targetPlaylistUrl = Db.getTargetPlaylistUrl env userId

    do! Spotify.replaceTracksInPlaylist env userId targetPlaylistUrl tracksIds

    return ()
  }
