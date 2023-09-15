module Domain.Core

open System.Threading.Tasks

type UserId = UserId of int64
type PlaylistId = PlaylistId of string
type ReadablePlaylistId = ReadablePlaylistId of PlaylistId
type WritablePlaylistId = WritablePlaylistId of PlaylistId
type TrackId = TrackId of string

type SpotifyPlaylistData =
  { Id: PlaylistId
    Name: string }

type ReadableSpotifyPlaylist = ReadableSpotifyPlaylist of SpotifyPlaylistData
type WriteableSpotifyPlaylist = WriteableSpotifyPlaylist of SpotifyPlaylistData

type SpotifyPlaylist =
  | Readable of ReadableSpotifyPlaylist
  | Writable of WriteableSpotifyPlaylist

type IncludedPlaylist =
  { Id: ReadablePlaylistId
    Name: string
    Enabled: bool }

type ExcludedPlaylist =
  { Id: ReadablePlaylistId
    Name: string
    Enabled: bool }

type TargetedPlaylistId = WritablePlaylistId

type TargetedPlaylist =
  { Id: TargetedPlaylistId
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

type SimplePreset = { Id: PresetId; Name: string }

type Preset =
  { Id: PresetId
    Name: string
    Settings: PresetSettings.PresetSettings
    IncludedPlaylists: IncludedPlaylist list
    ExcludedPlaylist: ExcludedPlaylist list
    TargetedPlaylists: TargetedPlaylist list
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

  type IncludePlaylist = RawPlaylistId -> Async<Result<IncludedPlaylist, IncludePlaylistError>>
  type ExcludePlaylist = RawPlaylistId -> Async<Result<ExcludedPlaylist, ExcludePlaylistError>>
  type TargetPlaylist = RawPlaylistId -> Async<Result<TargetedPlaylist, TargetPlaylistError>>

  type GenerateError = GenerateError of string
  type Generate = PresetId -> Async<Result<unit, GenerateError>>

[<RequireQualifiedAccess>]
module Preset =

  [<RequireQualifiedAccess>]
  type ValidationError =
    | NoIncludedPlaylists
    | NoTargetedPlaylists

  [<RequireQualifiedAccess>]
  type ValidationResult =
    | Ok
    | Errors of ValidationError list

  type Validate = Preset -> ValidationResult

  type SetLikedTracksHandling = PresetId -> PresetSettings.LikedTracksHandling -> Task<unit>

[<RequireQualifiedAccess>]
module User =
  type ListPresets = UserId -> Async<SimplePreset seq>
  type SetCurrentPreset = UserId -> PresetId -> Task<unit>

[<RequireQualifiedAccess>]
module IncludedPlaylist =
  type Enable = PresetId -> ReadablePlaylistId -> Task<unit>
  type Disable = PresetId -> ReadablePlaylistId -> Task<unit>

  let fromSpotifyPlaylist =
    function
    | Readable(ReadableSpotifyPlaylist { Id = id; Name = name }) ->
      { Id = (id |> ReadablePlaylistId)
        Name = name
        Enabled = true } : IncludedPlaylist
    | Writable(WriteableSpotifyPlaylist { Id = id; Name = name }) ->
      { Id = (id |> ReadablePlaylistId)
        Name = name
        Enabled = true }

[<RequireQualifiedAccess>]
module ExcludedPlaylist =
  let fromSpotifyPlaylist =
    function
    | Readable(ReadableSpotifyPlaylist { Id = id; Name = name }) ->
      { Id = (id |> ReadablePlaylistId)
        Name = name
        Enabled = true }
    | Writable(WriteableSpotifyPlaylist { Id = id; Name = name }) ->
      { Id = (id |> ReadablePlaylistId)
        Name = name
        Enabled = true }

[<RequireQualifiedAccess>]
module TargetedPlaylist =
  type Remove = PresetId -> TargetedPlaylistId -> Task<unit>
  type AppendTracks = PresetId -> TargetedPlaylistId -> Task<unit>
  type OverwriteTracks = PresetId -> TargetedPlaylistId -> Task<unit>

  let fromSpotifyPlaylist =
    function
    | Readable _ -> None
    | Writable(WriteableSpotifyPlaylist { Id = id; Name = name }) ->
      { Id = (id |> WritablePlaylistId)
        Name = name
        Enabled = true
        Overwrite = true }
      |> Some
