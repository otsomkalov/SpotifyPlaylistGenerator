module Infrastructure.Mapping

open System
open Database.Entities
open Infrastructure.Core
open Domain.Core
open Domain.Workflows

[<RequireQualifiedAccess>]
module SimplePreset =
  let fromDb (preset: Database.Entities.SimplePreset) : SimplePreset =
    { Id = preset.Id |> PresetId
      Name = preset.Name }

  let toDb (preset: SimplePreset) : Database.Entities.SimplePreset =
    Database.Entities.SimplePreset(Id = (preset.Id |> PresetId.value), Name = preset.Name)

  let toFullDb (userId: UserId) (preset: SimplePreset) : Database.Entities.Preset =
    Database.Entities.Preset(
      Id = (preset.Id |> PresetId.value),
      Name = preset.Name,
      UserId = (userId |> UserId.value),
      Settings = Database.Entities.Settings(
        PlaylistSize = 10,
        IncludeLikedTracks = Nullable true,
        RecommendationsEnabled = false))

[<RequireQualifiedAccess>]
module User =
  let fromDb (user: Database.Entities.User) : User =
    { Id = user.Id |> UserId
      CurrentPresetId =
        if isNull user.CurrentPresetId then
          None
        else
          Some(user.CurrentPresetId |> PresetId)
      Presets = user.Presets |> Seq.map SimplePreset.fromDb |> Seq.toList }

  let toDb (user: User) : Database.Entities.User =
    Database.Entities.User(
      Id = (user.Id |> UserId.value),
      CurrentPresetId = (user.CurrentPresetId |> Option.map PresetId.value |> Option.toObj),
      Presets = (user.Presets |> Seq.map SimplePreset.toDb)
    )

[<RequireQualifiedAccess>]
module IncludedPlaylist =
  let fromDb (playlist: Database.Entities.IncludedPlaylist) : IncludedPlaylist =
    { Id = playlist.Id |> PlaylistId |> ReadablePlaylistId
      Name = playlist.Name
      Enabled = not playlist.Disabled }

  let toDb (playlist: IncludedPlaylist) : Database.Entities.IncludedPlaylist =
    Database.Entities.IncludedPlaylist(
      Id = (playlist.Id |> ReadablePlaylistId.value |> PlaylistId.value),
      Name = playlist.Name,
      Disabled = not playlist.Enabled
    )

[<RequireQualifiedAccess>]
module ExcludedPlaylist =
  let fromDb (playlist: Database.Entities.ExcludedPlaylist) : ExcludedPlaylist =
    { Id = playlist.Id |> PlaylistId |> ReadablePlaylistId
      Name = playlist.Name
      Enabled = not playlist.Disabled }

  let toDb (playlist: ExcludedPlaylist) : Database.Entities.ExcludedPlaylist =
    Database.Entities.ExcludedPlaylist(
      Id = (playlist.Id |> ReadablePlaylistId.value |> PlaylistId.value),
      Name = playlist.Name,
      Disabled = not playlist.Enabled
    )

[<RequireQualifiedAccess>]
module TargetedPlaylist =
  let private fromDb (playlist: Database.Entities.TargetedPlaylist) : TargetedPlaylist =
    { Id = playlist.Id |> PlaylistId |> WritablePlaylistId
      Name = playlist.Name
      Overwrite = playlist.Overwrite
      Enabled = not playlist.Disabled }

  let toDb (playlist: TargetedPlaylist) : Database.Entities.TargetedPlaylist =
    Database.Entities.TargetedPlaylist(
      Id = (playlist.Id |> WritablePlaylistId.value |> PlaylistId.value),
      Name = playlist.Name,
      Overwrite = playlist.Overwrite,
      Disabled = not playlist.Enabled
    )

  let mapPlaylists (playlists: Database.Entities.TargetedPlaylist seq) =
    playlists |> Seq.map fromDb |> Seq.toList

module PresetSettings =
  let fromDb (settings: Settings) : PresetSettings.PresetSettings =
    { LikedTracksHandling =
        (match settings.IncludeLikedTracks |> Option.ofNullable with
         | Some true -> PresetSettings.LikedTracksHandling.Include
         | Some false -> PresetSettings.LikedTracksHandling.Exclude
         | None -> PresetSettings.LikedTracksHandling.Ignore)
      PlaylistSize = settings.PlaylistSize |> PlaylistSize.create
      RecommendationsEnabled = settings.RecommendationsEnabled }

  let toDb (settings: PresetSettings.PresetSettings) : Settings =
    Settings(
      IncludeLikedTracks =
        (match settings.LikedTracksHandling with
         | PresetSettings.LikedTracksHandling.Include -> Nullable true
         | PresetSettings.LikedTracksHandling.Exclude -> Nullable false
         | PresetSettings.LikedTracksHandling.Ignore -> Nullable<bool>()),
      PlaylistSize = (settings.PlaylistSize |> PlaylistSize.value),
      RecommendationsEnabled = settings.RecommendationsEnabled
    )

[<RequireQualifiedAccess>]
module Preset =
  let fromDb (preset: Database.Entities.Preset) : Preset =
    let mapIncludedPlaylist playlists =
      playlists |> Seq.map IncludedPlaylist.fromDb |> Seq.toList

    let mapExcludedPlaylist playlists =
      playlists |> Seq.map ExcludedPlaylist.fromDb |> Seq.toList

    { Id = preset.Id |> PresetId
      Name = preset.Name
      IncludedPlaylists = mapIncludedPlaylist preset.IncludedPlaylists
      ExcludedPlaylist = mapExcludedPlaylist preset.ExcludedPlaylists
      TargetedPlaylists = TargetedPlaylist.mapPlaylists preset.TargetedPlaylists
      Settings = PresetSettings.fromDb preset.Settings
      UserId = preset.UserId |> UserId }

  let toDb (preset: Domain.Core.Preset) : Database.Entities.Preset =
    Database.Entities.Preset(
      Id = (preset.Id |> PresetId.value),
      Name = preset.Name,
      UserId = (preset.UserId |> UserId.value),
      Settings = (preset.Settings |> PresetSettings.toDb),
      IncludedPlaylists = (preset.IncludedPlaylists |> Seq.map IncludedPlaylist.toDb),
      ExcludedPlaylists = (preset.ExcludedPlaylist |> Seq.map ExcludedPlaylist.toDb),
      TargetedPlaylists = (preset.TargetedPlaylists |> Seq.map TargetedPlaylist.toDb)
    )
