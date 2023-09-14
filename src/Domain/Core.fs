module Domain.Core

open System.Threading.Tasks

type UserId = UserId of int64
type PlaylistId = PlaylistId of string
type ReadablePlaylistId = ReadablePlaylistId of PlaylistId
type WritablePlaylistId = WritablePlaylistId of PlaylistId
type TrackId = TrackId of string

type SpotifyPlaylist =
  { Id: PlaylistId
    Name: string
    OwnerId: string }

type ReadablePlaylist ={
  Id: ReadablePlaylistId
  Name: string
  Enabled: bool
}

[<RequireQualifiedAccess>]
module ReadablePlaylist =
  let fromSpotifyPlaylist (spotifyPlaylist: SpotifyPlaylist) =
    {
      Id = spotifyPlaylist.Id |> ReadablePlaylistId
      Name = spotifyPlaylist.Name
      Enabled = true
    }

type WritablePlaylist ={
  Id: WritablePlaylistId
  Name: string
}

type IncludedPlaylist = ReadablePlaylist
type ExcludedPlaylist = ReadablePlaylist

[<RequireQualifiedAccess>]
module WritablePlaylist =
  let fromSpotifyPlaylist (spotifyPlaylist: SpotifyPlaylist) =
    {
      Id = spotifyPlaylist.Id |> WritablePlaylistId
      Name = spotifyPlaylist.Name
    }

type TargetPlaylistId = WritablePlaylistId

type TargetPlaylist =
  { Id: TargetPlaylistId
    Name: string
    Enabled: bool
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

[<RequireQualifiedAccess>]
module PlaylistSize =
  let value (PresetSettings.PlaylistSize size) = size

type PresetId = PresetId of int

type SimplePreset ={
  Id: PresetId
  Name: string
}

type Preset =
  { Id: PresetId
    Name: string
    Settings: PresetSettings.PresetSettings
    IncludedPlaylists: IncludedPlaylist list
    ExcludedPlaylist: ExcludedPlaylist list
    TargetPlaylists: TargetPlaylist list
    UserId: UserId }

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

  type IncludePlaylist = RawPlaylistId -> Async<Result<ReadablePlaylist, IncludePlaylistError>>
  type ExcludePlaylist = RawPlaylistId -> Async<Result<ReadablePlaylist, ExcludePlaylistError>>
  type TargetPlaylist = RawPlaylistId -> Async<Result<WritablePlaylist, TargetPlaylistError>>

  type GenerateError = GenerateError of string
  type Generate = PresetId -> Async<Result<unit, GenerateError>>

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

  type SetLikedTracksHandling = PresetId -> PresetSettings.LikedTracksHandling -> Task<unit>

[<RequireQualifiedAccess>]
module TargetPlaylist =
  type Remove = PresetId -> TargetPlaylistId -> Task<unit>
  type AppendTracks = PresetId -> TargetPlaylistId -> Task<unit>
  type OverwriteTracks = PresetId -> TargetPlaylistId -> Task<unit>

[<RequireQualifiedAccess>]
module IncludedPlaylist =
  type Enable = PresetId -> ReadablePlaylistId -> Task<unit>
  type Disable = PresetId -> ReadablePlaylistId -> Task<unit>