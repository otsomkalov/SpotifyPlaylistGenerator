module Domain.Tests.TargetedPlaylist

open Domain.Core

let mock: TargetedPlaylist =
  { Id = WritablePlaylistId(PlaylistId("targeted-playlist-id"))
    Enabled = true
    Name = "targeted-playlist-name"
    Overwrite = true }
