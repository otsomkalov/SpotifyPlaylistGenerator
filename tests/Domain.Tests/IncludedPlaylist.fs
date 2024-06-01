module Domain.Tests.IncludedPlaylist

open Domain.Core

let mock : IncludedPlaylist = {
  Id = ReadablePlaylistId(PlaylistId("playlist-id"))
  Name = "playlist-name"
  Enabled = true
}