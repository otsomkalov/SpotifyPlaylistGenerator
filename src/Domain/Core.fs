module Domain.Core

open System
open System.Threading.Tasks
open otsom.fs.Core

type PlaylistId = PlaylistId of string
type ReadablePlaylistId = ReadablePlaylistId of PlaylistId
type WritablePlaylistId = WritablePlaylistId of PlaylistId
type TrackId = TrackId of string

type ArtistId = ArtistId of string

type Artist = { Id: ArtistId; }

type Track = { Id: TrackId; Artists: Set<Artist> }

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

type PresetId = PresetId of string

[<RequireQualifiedAccess>]
module PresetSettings =
  [<RequireQualifiedAccess>]
  type LikedTracksHandling =
    | Include
    | Exclude
    | Ignore

  type RawPlaylistSize = RawPlaylistSize of string

  type PlaylistSize = private PlaylistSize of int

  type PresetSettings =
    { LikedTracksHandling: LikedTracksHandling
      PlaylistSize: PlaylistSize
      RecommendationsEnabled: bool
      UniqueArtists: bool }

  [<RequireQualifiedAccess>]
  module PlaylistSize =
    type ParsingError =
      | NotANumber
      | TooSmall
      | TooBig

    let tryParse (RawPlaylistSize size) =
      match Int32.TryParse size with
      | true, s when s >= 10000 -> Error(TooBig)
      | true, s when s <= 0 -> Error(TooSmall)
      | true, s -> Ok(PlaylistSize(s))
      | _ -> Error(NotANumber)

    let create size = PlaylistSize(size)
    let value (PlaylistSize size) = size

  type EnableUniqueArtists = PresetId -> Task<unit>
  type DisableUniqueArtists = PresetId -> Task<unit>

  type EnableRecommendations = PresetId -> Task<unit>
  type DisableRecommendations = PresetId -> Task<unit>

  type IncludeLikedTracks = PresetId -> Task<unit>
  type ExcludeLikedTracks = PresetId -> Task<unit>
  type IgnoreLikedTracks = PresetId -> Task<unit>

  type SetTargetPlaylistSize = PresetId -> RawPlaylistSize -> Task<Result<unit, PlaylistSize.ParsingError>>

type SimplePreset = { Id: PresetId; Name: string }

type Preset =
  { Id: PresetId
    Name: string
    Settings: PresetSettings.PresetSettings
    IncludedPlaylists: IncludedPlaylist list
    ExcludedPlaylists: ExcludedPlaylist list
    TargetedPlaylists: TargetedPlaylist list }

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

[<RequireQualifiedAccess>]
module Preset =
  type Get = PresetId -> Task<Preset>

  [<RequireQualifiedAccess>]
  type ValidationError =
    | NoIncludedPlaylists
    | NoTargetedPlaylists

  type Validate = Preset -> Result<Preset, ValidationError list>
  type Create = string -> Task<PresetId>
  type Remove = PresetId -> Task<unit>

  type GenerateError =
    | NoIncludedTracks
    | NoPotentialTracks

  type Generate = PresetId -> Task<Result<unit, GenerateError>>

[<RequireQualifiedAccess>]
module User =
  type Get = UserId -> Task<User>
  type SetCurrentPreset = UserId -> PresetId -> Task<unit>
  type RemovePreset = UserId -> PresetId -> Task<unit>
  type CreateIfNotExists = UserId -> Task<unit>
  type SetCurrentPresetSize = UserId -> PresetSettings.RawPlaylistSize -> Task<Result<unit, PresetSettings.PlaylistSize.ParsingError>>
  type GenerateCurrentPreset = UserId -> Task<Result<unit, Preset.GenerateError>>

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
