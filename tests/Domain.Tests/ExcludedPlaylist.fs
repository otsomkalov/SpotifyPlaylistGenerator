module Domain.Tests.ExcludedPlaylist

open Domain.Core

let mock: ExcludedPlaylist =
  { Id = ReadablePlaylistId(PlaylistId("playlist-id"))
    Name = "playlist-name"
    Enabled = true }
