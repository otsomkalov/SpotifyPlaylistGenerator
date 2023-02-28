module Infrastructure.Mapping

open System
open Database.Entities
open Domain.Core
open Infrastructure.Core

[<RequireQualifiedAccess>]
module ReadablePlaylistId =
  let filterMapPlaylistsIds (playlists: Playlist seq) type' =
    playlists
    |> Seq.where (fun p -> p.PlaylistType = type')
    |> Seq.map (fun p -> ReadablePlaylistId p.Url)
    |> Seq.toList

[<RequireQualifiedAccess>]
module WritablePlaylistId =
  let filterMapPlaylistsIds (playlists: Playlist seq) =
    playlists
    |> Seq.where (fun p ->
      p.PlaylistType = PlaylistType.Target)
    |> Seq.map (fun p -> WritablePlaylistId p.Url)
    |> Seq.toList

module User =
  let fromDb userId (playlists: Playlist seq) =
    { Id = userId
      IncludedPlaylists = ReadablePlaylistId.filterMapPlaylistsIds playlists PlaylistType.Source
      TargetPlaylists = WritablePlaylistId.filterMapPlaylistsIds playlists }

module UserSettings =
  let fromDb (settings: Settings) : UserSettings.UserSettings =
    { LikedTracksHandling =
        (match settings.IncludeLikedTracks |> Option.ofNullable with
         | Some v when v = true -> UserSettings.LikedTracksHandling.Include
         | Some v when v = false -> UserSettings.LikedTracksHandling.Exclude
         | None -> UserSettings.LikedTracksHandling.Ignore)
      PlaylistSize = PlaylistSize.create settings.PlaylistSize }

  let toDb (settings: UserSettings.UserSettings) : Settings =
    Settings(
      IncludeLikedTracks =
        (match settings.LikedTracksHandling with
         | UserSettings.LikedTracksHandling.Include -> Nullable true
         | UserSettings.LikedTracksHandling.Exclude -> Nullable false
         | UserSettings.LikedTracksHandling.Ignore -> Nullable<bool>()),
      PlaylistSize = (settings.PlaylistSize |> PlaylistSize.value)
    )
