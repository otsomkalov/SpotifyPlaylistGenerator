module Infrastructure.Mapping

open System
open Database.Entities
open Infrastructure.Core
open Domain.Core

[<RequireQualifiedAccess>]
module ReadablePlaylistId =
  let filterMapPlaylistsIds (playlists: SourcePlaylist seq) type' =
    playlists
    |> Seq.where (fun p -> p.PlaylistType = type')
    |> Seq.map (fun p -> ReadablePlaylistId p.Url)
    |> Seq.toList

[<RequireQualifiedAccess>]
module WritablePlaylistId =
  let filterMapPlaylistsIds (playlists: TargetPlaylist seq) =
    playlists |> Seq.map (fun p -> WritablePlaylistId p.Url) |> Seq.toList

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
      IncludedPlaylists = ReadablePlaylistId.filterMapPlaylistsIds user.SourcePlaylists PlaylistType.Source
      ExcludedPlaylist = ReadablePlaylistId.filterMapPlaylistsIds user.SourcePlaylists PlaylistType.History
      TargetPlaylists = WritablePlaylistId.filterMapPlaylistsIds user.TargetPlaylists
      Settings = UserSettings.fromDb user.Settings }