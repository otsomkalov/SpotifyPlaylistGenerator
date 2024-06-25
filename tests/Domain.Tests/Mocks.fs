module Domain.Tests.Mocks

open Domain.Core

let includedPlaylistMock: IncludedPlaylist =
  { Id = ReadablePlaylistId(PlaylistId("included-playlist-id"))
    Name = "included-playlist-name"
    Enabled = true }

let excludedPlaylistMock: ExcludedPlaylist =
  { Id = ReadablePlaylistId(PlaylistId("excluded-playlist-id"))
    Name = "excluded-playlist-name"
    Enabled = true }

let targetedPlaylistMock: TargetedPlaylist =
  { Id = WritablePlaylistId(PlaylistId("targeted-playlist-id"))
    Enabled = true
    Name = "targeted-playlist-name"
    Overwrite = true }

let presetSettingsMock: PresetSettings.PresetSettings =
  { PlaylistSize = PresetSettings.PlaylistSize.create 10
    RecommendationsEnabled = false
    LikedTracksHandling = PresetSettings.LikedTracksHandling.Include
    UniqueArtists = false }

let presetMockId = PresetId("1")

let presetMock =
  { Id = presetMockId
    Name = "test-preset-name"
    IncludedPlaylists = [ includedPlaylistMock ]
    ExcludedPlaylists = [ excludedPlaylistMock ]
    TargetedPlaylists = [ targetedPlaylistMock ]
    Settings = presetSettingsMock }