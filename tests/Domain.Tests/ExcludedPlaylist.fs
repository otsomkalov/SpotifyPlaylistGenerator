module Domain.Tests.ExcludedPlaylist

open Domain.Core

let mock: ExcludedPlaylist =
  { Id = ReadablePlaylistId(PlaylistId("excluded-playlist-id"))
    Name = "excluded-playlist-name"
    Enabled = true }
