module Domain.Core

open System.Threading.Tasks

type UserId = UserId of int64
type PlaylistId = PlaylistId of string
type ReadablePlaylistId = ReadablePlaylistId of PlaylistId
type WritablePlaylistId = WritablePlaylistId of PlaylistId
type TrackId = TrackId of string

type SpotifyPlaylist =
  { Id: PlaylistId
    OwnerId: string }

type TargetPlaylist =
  { Id: WritablePlaylistId
    Overwrite: bool }

[<RequireQualifiedAccess>]
module PresetSettings =
  [<RequireQualifiedAccess>]
  type LikedTracksHandling =
    | Include
    | Exclude
    | Ignore

  type PlaylistSize = PlaylistSize of int

  type PresetSettings =
    { LikedTracksHandling: LikedTracksHandling
      PlaylistSize: PlaylistSize }

  type SetPlaylistSize = UserId -> PlaylistSize -> Task
  type SetLikedTracksHandling = UserId -> LikedTracksHandling -> Task

type PresetId = PresetId of int

type SimplePreset ={
  Id: PresetId
  Name: string
}

type Preset =
  { Id: PresetId
    Settings: PresetSettings.PresetSettings
    IncludedPlaylists: ReadablePlaylistId list
    ExcludedPlaylist: ReadablePlaylistId list
    TargetPlaylists: TargetPlaylist list }

type User = { Id: UserId }

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

  type AccessError = AccessError of unit

  type TargetPlaylistError =
    | IdParsing of IdParsingError
    | MissingFromSpotify of MissingFromSpotifyError
    | AccessError of AccessError

  type IncludePlaylist = RawPlaylistId -> Async<Result<unit, IncludePlaylistError>>
  type ExcludePlaylist = RawPlaylistId -> Async<Result<unit, ExcludePlaylistError>>
  type TargetPlaylist = RawPlaylistId -> Async<Result<WritablePlaylistId, TargetPlaylistError>>

[<RequireQualifiedAccess>]
module Preset =

  [<RequireQualifiedAccess>]
  type ValidationError =
    | NoIncludedPlaylists
    | NoTargetPlaylists

  [<RequireQualifiedAccess>]
  type ValidationResult =
    | Ok
    | Errors of ValidationError list

  type Validate = Preset -> ValidationResult