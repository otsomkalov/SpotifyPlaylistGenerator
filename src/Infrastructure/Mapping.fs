module Infrastructure.Mapping

open System
open Database.Entities
open Infrastructure.Core
open Domain.Core

[<RequireQualifiedAccess>]
module ReadablePlaylistId =
  let fromDb (playlists: #Playlist seq) =
    playlists |> Seq.map (fun p -> ReadablePlaylistId p.Url) |> Seq.toList

[<RequireQualifiedAccess>]
module TargetPlaylist =
  let private fromDb (playlist: Database.Entities.TargetPlaylist) : TargetPlaylist =
    { Id = playlist.Url |> WritablePlaylistId
      Overwrite = playlist.Overwrite }

  let mapPlaylists (playlists: Database.Entities.TargetPlaylist seq) =
    playlists |> Seq.map fromDb |> Seq.toList

module UserSettings =
  let fromDb (settings: Settings) : UserSettings.UserSettings =
    { LikedTracksHandling =
        (match settings.IncludeLikedTracks |> Option.ofNullable with
         | Some v when v = true -> UserSettings.LikedTracksHandling.Include
         | Some v when v = false -> UserSettings.LikedTracksHandling.Exclude
         | None -> UserSettings.LikedTracksHandling.Ignore)
      PlaylistSize = settings.PlaylistSize |> PlaylistSize.create }

  let toDb (settings: UserSettings.UserSettings) : Settings =
    Settings(
      IncludeLikedTracks =
        (match settings.LikedTracksHandling with
         | UserSettings.LikedTracksHandling.Include -> Nullable true
         | UserSettings.LikedTracksHandling.Exclude -> Nullable false
         | UserSettings.LikedTracksHandling.Ignore -> Nullable<bool>()),
      PlaylistSize = (settings.PlaylistSize |> PlaylistSize.value)
    )

[<RequireQualifiedAccess>]
module User =
  let fromDb (user: Database.Entities.User) =
    { Id = user.Id |> UserId
      IncludedPlaylists = ReadablePlaylistId.fromDb user.SourcePlaylists
      ExcludedPlaylist = ReadablePlaylistId.fromDb user.HistoryPlaylists
      TargetPlaylists = TargetPlaylist.mapPlaylists user.TargetPlaylists
      Settings = UserSettings.fromDb user.Settings }
