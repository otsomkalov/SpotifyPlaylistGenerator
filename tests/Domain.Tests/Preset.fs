module Domain.Tests.Preset

open Domain.Core

let presetMock =
  { Id = PresetId("1")
    Name = "test"
    IncludedPlaylists = []
    ExcludedPlaylists = []
    TargetedPlaylists = []
    Settings =
      { PlaylistSize = PresetSettings.PlaylistSize.create 10
        RecommendationsEnabled = false
        LikedTracksHandling = PresetSettings.LikedTracksHandling.Include
        UniqueArtists = false } }