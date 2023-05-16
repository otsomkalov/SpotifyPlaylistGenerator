module Domain.Core

open System.Threading.Tasks

type UserId = UserId of int64
type ReadablePlaylistId = ReadablePlaylistId of string
type WritablePlaylistId = WritablePlaylistId of string
type TrackId = TrackId of string

type TargetPlaylist = {
  Id: WritablePlaylistId
  Overwrite: bool
}

[<RequireQualifiedAccess>]
module ValidateUserPlaylists =
  type Error =
    | NoIncludedPlaylists
    | NoTargetPlaylists

  type Result =
    | Ok
    | Errors of Error list

  type Action = UserId -> Task<Result>

[<RequireQualifiedAccess>]
module UserSettings =
  [<RequireQualifiedAccess>]
  type LikedTracksHandling =
    | Include
    | Exclude
    | Ignore

  type PlaylistSize = PlaylistSize of int

  type UserSettings =
    { LikedTracksHandling: LikedTracksHandling
      PlaylistSize: PlaylistSize }

  type SetPlaylistSize = UserId -> PlaylistSize -> Task
  type SetLikedTracksHandling = UserId -> LikedTracksHandling -> Task

type User =
  { Id: UserId
    IncludedPlaylists: ReadablePlaylistId list
    ExcludedPlaylist: ReadablePlaylistId list
    TargetPlaylists: TargetPlaylist list
    Settings: UserSettings.UserSettings }

[<RequireQualifiedAccess>]
module Playlist =
  type RawPlaylistId = RawPlaylistId of string
  type IdParsingError = IdParsingError of unit
  type MissingFromSpotifyError = MissingFromSpotifyError of string

  type IncludePlaylistError =
    | IdParsing of IdParsingError
    | MissingFromSpotify of MissingFromSpotifyError

  type ExcludePlaylistError =
    | IdParsing of IdParsingError
    | MissingFromSpotify of MissingFromSpotifyError

  type IncludePlaylist = RawPlaylistId -> Async<Result<unit, IncludePlaylistError>>
  type ExcludePlaylist = RawPlaylistId -> Async<Result<unit, ExcludePlaylistError>>
