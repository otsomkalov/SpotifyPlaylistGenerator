module Domain.Tests.PresetSettings

open Domain.Core

let mock: PresetSettings.PresetSettings =
  { PlaylistSize = PresetSettings.PlaylistSize.create 10
    RecommendationsEnabled = false
    LikedTracksHandling = PresetSettings.LikedTracksHandling.Include
    UniqueArtists = false }
