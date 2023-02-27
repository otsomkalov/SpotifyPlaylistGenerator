module Infrastructure.Mapping

open Database.Entities
open Domain.Core

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
      p.PlaylistType = PlaylistType.Target
      || p.PlaylistType = PlaylistType.TargetHistory)
    |> Seq.map (fun p -> WritablePlaylistId p.Url)
    |> Seq.toList

module User =
  let fromDb userId (playlists: Playlist seq) =
    { Id = userId
      IncludedPlaylists = ReadablePlaylistId.filterMapPlaylistsIds playlists PlaylistType.Source
      TargetPlaylists = WritablePlaylistId.filterMapPlaylistsIds playlists }
