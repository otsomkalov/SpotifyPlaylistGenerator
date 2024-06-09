module Domain.Tests.IncludedPlaylist

open Domain.Core

let mock: IncludedPlaylist =
  { Id = ReadablePlaylistId(PlaylistId("included-playlist-id"))
    Name = "included-playlist-name"
    Enabled = true }
