module internal Infrastructure.Mapping

open System
open Database
open Domain.Core
open Domain.Workflows
open otsom.fs.Core

[<RequireQualifiedAccess>]
module SimplePreset =
  let fromDb (preset: Entities.SimplePreset) : SimplePreset =
    { Id = preset.Id |> PresetId
      Name = preset.Name }

  let toDb (preset: SimplePreset) : Entities.SimplePreset =
    Entities.SimplePreset(Id = (preset.Id |> PresetId.value), Name = preset.Name)

[<RequireQualifiedAccess>]
module User =
  let fromDb (user: Entities.User) : User =
    { Id = user.Id |> UserId
      CurrentPresetId =
        if isNull user.CurrentPresetId then
          None
        else
          Some(user.CurrentPresetId |> PresetId)
      Presets = user.Presets |> Seq.map SimplePreset.fromDb |> Seq.toList }

  let toDb (user: User) : Entities.User =
    Entities.User(
      Id = (user.Id |> UserId.value),
      CurrentPresetId = (user.CurrentPresetId |> Option.map PresetId.value |> Option.toObj),
      Presets = (user.Presets |> Seq.map SimplePreset.toDb)
    )

[<RequireQualifiedAccess>]
module IncludedPlaylist =
  let fromDb (playlist: Entities.IncludedPlaylist) : IncludedPlaylist =
    { Id = playlist.Id |> PlaylistId |> ReadablePlaylistId
      Name = playlist.Name
      Enabled = not playlist.Disabled
      LikedOnly = playlist.LikedOnly }

  let toDb (playlist: IncludedPlaylist) : Entities.IncludedPlaylist =
    Entities.IncludedPlaylist(
      Id = (playlist.Id |> ReadablePlaylistId.value |> PlaylistId.value),
      Name = playlist.Name,
      Disabled = not playlist.Enabled
    )

[<RequireQualifiedAccess>]
module ExcludedPlaylist =
  let fromDb (playlist: Entities.ExcludedPlaylist) : ExcludedPlaylist =
    { Id = playlist.Id |> PlaylistId |> ReadablePlaylistId
      Name = playlist.Name
      Enabled = not playlist.Disabled }

  let toDb (playlist: ExcludedPlaylist) : Entities.ExcludedPlaylist =
    Entities.ExcludedPlaylist(
      Id = (playlist.Id |> ReadablePlaylistId.value |> PlaylistId.value),
      Name = playlist.Name,
      Disabled = not playlist.Enabled
    )

[<RequireQualifiedAccess>]
module TargetedPlaylist =
  let private fromDb (playlist: Entities.TargetedPlaylist) : TargetedPlaylist =
    { Id = playlist.Id |> PlaylistId |> WritablePlaylistId
      Name = playlist.Name
      Overwrite = playlist.Overwrite
      Enabled = not playlist.Disabled }

  let toDb (playlist: TargetedPlaylist) : Entities.TargetedPlaylist =
    Entities.TargetedPlaylist(
      Id = (playlist.Id |> WritablePlaylistId.value |> PlaylistId.value),
      Name = playlist.Name,
      Overwrite = playlist.Overwrite,
      Disabled = not playlist.Enabled
    )

  let mapPlaylists (playlists: Entities.TargetedPlaylist seq) =
    playlists |> Seq.map fromDb |> Seq.toList

module PresetSettings =
  let fromDb (settings: Entities.Settings) : PresetSettings.PresetSettings =
    { LikedTracksHandling =
        (match settings.IncludeLikedTracks |> Option.ofNullable with
         | Some true -> PresetSettings.LikedTracksHandling.Include
         | Some false -> PresetSettings.LikedTracksHandling.Exclude
         | None -> PresetSettings.LikedTracksHandling.Ignore)
      Size = settings.Size |> PresetSettings.Size.create
      RecommendationsEnabled = settings.RecommendationsEnabled
      UniqueArtists = settings.UniqueArtists }

  let toDb (settings: PresetSettings.PresetSettings) : Entities.Settings =
    Entities.Settings(
      IncludeLikedTracks =
        (match settings.LikedTracksHandling with
         | PresetSettings.LikedTracksHandling.Include -> Nullable true
         | PresetSettings.LikedTracksHandling.Exclude -> Nullable false
         | PresetSettings.LikedTracksHandling.Ignore -> Nullable<bool>()),
      Size = (settings.Size |> PresetSettings.Size.value),
      RecommendationsEnabled = settings.RecommendationsEnabled,
      UniqueArtists = settings.UniqueArtists
    )

[<RequireQualifiedAccess>]
module Preset =
  let fromDb (preset: Entities.Preset) : Preset =
    let mapIncludedPlaylist playlists =
      playlists |> Seq.map IncludedPlaylist.fromDb |> Seq.toList

    let mapExcludedPlaylist playlists =
      playlists |> Seq.map ExcludedPlaylist.fromDb |> Seq.toList

    { Id = preset.Id |> PresetId
      Name = preset.Name
      IncludedPlaylists = mapIncludedPlaylist preset.IncludedPlaylists
      ExcludedPlaylists = mapExcludedPlaylist preset.ExcludedPlaylists
      TargetedPlaylists = TargetedPlaylist.mapPlaylists preset.TargetedPlaylists
      Settings = PresetSettings.fromDb preset.Settings }

  let toDb (preset: Domain.Core.Preset) : Entities.Preset =
    Entities.Preset(
      Id = (preset.Id |> PresetId.value),
      Name = preset.Name,
      Settings = (preset.Settings |> PresetSettings.toDb),
      IncludedPlaylists = (preset.IncludedPlaylists |> Seq.map IncludedPlaylist.toDb),
      ExcludedPlaylists = (preset.ExcludedPlaylists |> Seq.map ExcludedPlaylist.toDb),
      TargetedPlaylists = (preset.TargetedPlaylists |> Seq.map TargetedPlaylist.toDb)
    )
