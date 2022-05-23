﻿module Generator.Worker.Services.PlaylistsService

open Shared

let listTracksIdsAsync env userId refreshCache =
  task {
    let! playlistsUrls = Db.listUserSourcePlaylistsUrls env userId

    let! tracksIds = PlaylistService.listTracksIdsAsync env userId playlistsUrls refreshCache

    Log.info env ("User with Telegram id {TelegramId} has {PlaylistsTracksIdsCount} tracks in source playlists", [ userId; tracksIds.Length ])

    return tracksIds
  }
