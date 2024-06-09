module Domain.Tests.Preset

open Domain.Core

let mockId = PresetId("1")

let mock =
  { Id = mockId
    Name = "test-preset-name"
    IncludedPlaylists = [ IncludedPlaylist.mock ]
    ExcludedPlaylists = [ ExcludedPlaylist.mock ]
    TargetedPlaylists = [ TargetedPlaylist.mock ]
    Settings = PresetSettings.mock }
