module Domain.Core

open System
open System.Threading.Tasks
open shortid

type UserId = UserId of int64
type PlaylistId = PlaylistId of string
type ReadablePlaylistId = ReadablePlaylistId of PlaylistId
type WritablePlaylistId = WritablePlaylistId of PlaylistId
type TrackId = TrackId of string

[<RequireQualifiedAccess>]
module TrackId =
  let value (TrackId id) = id

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

  type PlaylistSize = private PlaylistSize of int

  type PresetSettings =
    { LikedTracksHandling: LikedTracksHandling
      PlaylistSize: PlaylistSize
      RecommendationsEnabled: bool }

  [<RequireQualifiedAccess>]
  module PlaylistSize =
    type TryCreateError =
      | TooSmall
      | TooBig

    let tryCreate size =
      match size with
      | s when s <= 0 -> Error(TooSmall)
      | s when s >= 10000 -> Error(TooBig)
      | _ -> Ok(PlaylistSize(size))

    let create size =
      match tryCreate size with
      | Ok size -> size
      | Error e ->
        ArgumentException(e |> string, nameof size) |> raise

    let value (PlaylistSize size) = size

type PresetId = PresetId of string

type SimplePreset = { Id: PresetId; Name: string }

type Preset =
  { Id: PresetId
    Name: string
    Settings: PresetSettings.PresetSettings
    IncludedPlaylists: IncludedPlaylist list
    ExcludedPlaylist: ExcludedPlaylist list
    TargetedPlaylists: TargetedPlaylist list
    UserId: UserId }

type User = {
  Id: UserId
  CurrentPresetId: PresetId option
  Presets: SimplePreset list
}

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

  type IncludePlaylist = PresetId -> RawPlaylistId -> Task<Result<IncludedPlaylist, IncludePlaylistError>>
  type ExcludePlaylist = PresetId -> RawPlaylistId -> Task<Result<ExcludedPlaylist, ExcludePlaylistError>>
  type TargetPlaylist = PresetId -> RawPlaylistId -> Task<Result<TargetedPlaylist, TargetPlaylistError>>

  type GenerateError =
    | NoIncludedTracks
    | NoPotentialTracks
  type Generate = PresetId -> Task<Result<unit, GenerateError>>

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

  type IncludeLikedTracks = PresetId -> Task<unit>
  type ExcludeLikedTracks = PresetId -> Task<unit>
  type IgnoreLikedTracks = PresetId -> Task<unit>
  type SetPlaylistSize = PresetId -> PresetSettings.PlaylistSize -> Task<unit>
  type Create = string -> Task<PresetId>
  type Remove = PresetId -> Task<Preset>

  type EnableRecommendations = PresetId -> Task<unit>
  type DisableRecommendations = PresetId -> Task<unit>

[<RequireQualifiedAccess>]
module User =
  type SetCurrentPreset = UserId -> PresetId -> Task<unit>

  let create userId =
    { Id = userId
      CurrentPresetId = None
      Presets = [] }

[<RequireQualifiedAccess>]
module IncludedPlaylist =
  type Enable = PresetId -> ReadablePlaylistId -> Task<unit>
  type Disable = PresetId -> ReadablePlaylistId -> Task<unit>
  type Remove = PresetId -> ReadablePlaylistId -> Task<unit>

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
  type Enable = PresetId -> ReadablePlaylistId -> Task<unit>
  type Disable = PresetId -> ReadablePlaylistId -> Task<unit>
  type Remove = PresetId -> ReadablePlaylistId -> Task<unit>

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
  type Enable = PresetId -> ReadablePlaylistId -> Task<unit>
  type Disable = PresetId -> ReadablePlaylistId -> Task<unit>
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
        Overwrite = false }
      |> Some

[<RequireQualifiedAccess>]
module Auth =
  type State = private State of string

  [<RequireQualifiedAccess>]
  module State =
    let create () = ShortId.Generate() |> State

    let parse str = State str

    let value (State key) = key

  type Inited = { State: State; UserId: UserId }

  type GetLoginLink = UserId -> Task<string>

  type Fulfilled =
    { UserId: UserId
      State: State
      Code: string }

  type FulfillmentError = | StateNotFound

  type Fulfill = State -> string -> Task<Result<Fulfilled, FulfillmentError>>

  type CompleteError =
    | StateNotFound
    | StateDoesntBelongToUser

  type Completed = { UserId: UserId; Token: string }

  type Complete = UserId -> State -> Task<Result<unit, CompleteError>>
