module Domain.Tests.Mocks

open Domain.Core
open MusicPlatform

let includedTrack =
  { Id = TrackId "included-track-id"
    Artists = Set.ofList [ { Id = ArtistId "1" }; { Id = ArtistId "2" } ] }

let excludedTrack =
  { Id = TrackId "excluded-track-id"
    Artists = Set.ofList [ { Id = ArtistId "2" }; { Id = ArtistId "3" } ] }

let likedTrack =
  { Id = TrackId "liked-track-id"
    Artists = Set.ofList [ { Id = ArtistId "3" }; { Id = ArtistId "4" } ] }

let recommendedTrack =
  { Id = TrackId "recommended-track-id"
    Artists = Set.ofList [ { Id = ArtistId "3" }; { Id = ArtistId "4" } ] }

let includedPlaylistId = PlaylistId("included-playlist-id")

let includedPlaylist: IncludedPlaylist =
  { Id = ReadablePlaylistId(includedPlaylistId)
    Name = "included-playlist-name"
    Enabled = true
    LikedOnly = false }

let excludedPlaylist: ExcludedPlaylist =
  { Id = ReadablePlaylistId(PlaylistId("excluded-playlist-id"))
    Name = "excluded-playlist-name"
    Enabled = true }

let targetedPlaylistId = PlaylistId("targeted-playlist-id")

let targetedPlaylist: TargetedPlaylist =
  { Id = WritablePlaylistId(targetedPlaylistId)
    Enabled = true
    Name = "targeted-playlist-name"
    Overwrite = true }

let presetSettingsMock: PresetSettings.PresetSettings =
  { Size = PresetSettings.Size.create 10
    RecommendationsEnabled = false
    LikedTracksHandling = PresetSettings.LikedTracksHandling.Ignore
    UniqueArtists = false }

let presetId = PresetId("1")

let preset =
  { Id = presetId
    Name = "test-preset-name"
    IncludedPlaylists = [ includedPlaylist ]
    ExcludedPlaylists = [ excludedPlaylist ]
    TargetedPlaylists = [ targetedPlaylist ]
    Settings = presetSettingsMock }
