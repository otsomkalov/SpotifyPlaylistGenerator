module Infrastructure.Mapping

open System
open Database.Entities
open Infrastructure.Core
open Domain.Core

[<RequireQualifiedAccess>]
module ReadablePlaylistId =
  let fromDb (playlists: #Playlist seq) =
    playlists
    |> Seq.map (fun p -> p.Url |> PlaylistId |> ReadablePlaylistId)
    |> Seq.toList

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
  let fromDb (user: Database.Entities.Preset) : Preset =
    { Id = user.Id |> PresetId
      IncludedPlaylists = ReadablePlaylistId.fromDb user.SourcePlaylists
      ExcludedPlaylist = ReadablePlaylistId.fromDb user.HistoryPlaylists
      TargetPlaylists = TargetPlaylist.mapPlaylists user.TargetPlaylists
      Settings = PresetSettings.fromDb user.Settings }
