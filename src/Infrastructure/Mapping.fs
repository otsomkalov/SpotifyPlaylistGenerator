module Infrastructure.Mapping

open System
open Database.Entities
open Infrastructure.Core
open Domain.Core

module ReadablePlaylistId =
  let fromDb (playlists: #Playlist seq) =
    playlists
    |> Seq.map (fun p -> p.Url |> PlaylistId |> ReadablePlaylistId)
    |> Seq.toList

[<RequireQualifiedAccess>]
module IncludedPlaylist =
  let fromDb (playlist: SourcePlaylist) : IncludedPlaylist =
    { Id = playlist.Url |> PlaylistId |> ReadablePlaylistId
      Name = playlist.Name }

[<RequireQualifiedAccess>]
module ExcludedPlaylist =
  let fromDb (playlist: HistoryPlaylist) : ExcludedPlaylist =
    { Id = playlist.Url |> PlaylistId |> ReadablePlaylistId
      Name = playlist.Name }

[<RequireQualifiedAccess>]
module TargetPlaylist =
  let private fromDb (playlist: Database.Entities.TargetPlaylist) : TargetPlaylist =
    { Id = playlist.Url |> PlaylistId |> WritablePlaylistId
      Overwrite = playlist.Overwrite }

  let mapPlaylists (playlists: Database.Entities.TargetPlaylist seq) =
    playlists |> Seq.map fromDb |> Seq.toList

module PresetSettings =
  let fromDb (settings: Settings) : PresetSettings.PresetSettings =
    { LikedTracksHandling =
        (match settings.IncludeLikedTracks |> Option.ofNullable with
         | Some true -> PresetSettings.LikedTracksHandling.Include
         | Some false -> PresetSettings.LikedTracksHandling.Exclude
         | None -> PresetSettings.LikedTracksHandling.Ignore)
      PlaylistSize = settings.PlaylistSize |> PlaylistSize.create }

  let toDb (settings: PresetSettings.PresetSettings) : Settings =
    Settings(
      IncludeLikedTracks =
        (match settings.LikedTracksHandling with
         | PresetSettings.LikedTracksHandling.Include -> Nullable true
         | PresetSettings.LikedTracksHandling.Exclude -> Nullable false
         | PresetSettings.LikedTracksHandling.Ignore -> Nullable<bool>()),
      PlaylistSize = (settings.PlaylistSize |> PlaylistSize.value)
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
      IncludedPlaylists = mapIncludedPlaylist preset.SourcePlaylists
      ExcludedPlaylist = mapExcludedPlaylist preset.HistoryPlaylists
      TargetPlaylists = TargetPlaylist.mapPlaylists preset.TargetPlaylists
      Settings = PresetSettings.fromDb preset.Settings
      UserId = preset.UserId |> UserId }

[<RequireQualifiedAccess>]
module SimplePreset =
  let fromDb (preset: Database.Entities.Preset) : SimplePreset =
    { Id = preset.Id |> PresetId
      Name = preset.Name }
