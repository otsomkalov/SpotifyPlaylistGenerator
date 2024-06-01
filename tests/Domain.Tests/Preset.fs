module Domain.Tests.Preset

open Domain.Core

let mockId = PresetId("1")

let mock =
  { Id = mockId
    Name = "test"
    IncludedPlaylists = []
    ExcludedPlaylists = []
    TargetedPlaylists = []
    Settings =
      { PlaylistSize = PresetSettings.PlaylistSize.create 10
        RecommendationsEnabled = false
        LikedTracksHandling = PresetSettings.LikedTracksHandling.Include
        UniqueArtists = false } }