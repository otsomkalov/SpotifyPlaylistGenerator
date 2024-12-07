module Domain.Core

open System
open System.Threading.Tasks
open MusicPlatform
open otsom.fs.Core

type ReadablePlaylistId = ReadablePlaylistId of PlaylistId
type WritablePlaylistId = WritablePlaylistId of PlaylistId

type IncludedPlaylist =
  { Id: ReadablePlaylistId
    Name: string
    Enabled: bool
    LikedOnly: bool }

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

  type RawPresetSize = RawPresetSize of string

  type Size = private Size of int

  type PresetSettings =
    { LikedTracksHandling: LikedTracksHandling
      Size: Size
      RecommendationsEnabled: bool
      UniqueArtists: bool }

  [<RequireQualifiedAccess>]
  module Size =
    type ParsingError =
      | NotANumber
      | TooSmall
      | TooBig

    let tryParse (RawPresetSize size) =
      match Int32.TryParse size with
      | true, s when s >= 10000 -> Error(TooBig)
      | true, s when s <= 0 -> Error(TooSmall)
      | true, s -> Ok(Size(s))
      | _ -> Error(NotANumber)

    let create size = Size(size)
    let value (Size size) = size

  type EnableUniqueArtists = PresetId -> Task<unit>
  type DisableUniqueArtists = PresetId -> Task<unit>

  type EnableRecommendations = PresetId -> Task<unit>
  type DisableRecommendations = PresetId -> Task<unit>

  type IncludeLikedTracks = PresetId -> Task<unit>
  type ExcludeLikedTracks = PresetId -> Task<unit>
  type IgnoreLikedTracks = PresetId -> Task<unit>

  type SetPresetSize = PresetId -> RawPresetSize -> Task<Result<unit, Size.ParsingError>>

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
  type IncludePlaylistError =
    | IdParsing of Playlist.IdParsingError
    | Load of Playlist.LoadError
    | Unauthorized

  type ExcludePlaylistError =
    | IdParsing of Playlist.IdParsingError
    | Load of Playlist.LoadError
    | Unauthorized

  type AccessError = AccessError of unit

  type TargetPlaylistError =
    | IdParsing of Playlist.IdParsingError
    | Load of Playlist.LoadError
    | AccessError of AccessError

  type IncludePlaylist = PresetId -> Playlist.RawPlaylistId -> Task<Result<IncludedPlaylist, IncludePlaylistError>>
  type ExcludePlaylist = PresetId -> Playlist.RawPlaylistId -> Task<Result<ExcludedPlaylist, ExcludePlaylistError>>
  type TargetPlaylist = PresetId -> Playlist.RawPlaylistId -> Task<Result<TargetedPlaylist, TargetPlaylistError>>

[<RequireQualifiedAccess>]
module Preset =
  type Get = PresetId -> Task<Preset>

  [<RequireQualifiedAccess>]
  type ValidationError =
    | NoIncludedPlaylists
    | NoTargetedPlaylists

  type Validate = Preset -> Result<Preset, ValidationError list>
  type Create = string -> Task<Preset>
  type Remove = PresetId -> Task<unit>

  type RunError =
    | NoIncludedTracks
    | NoPotentialTracks

  type Run = PresetId -> Task<Result<Preset, RunError>>

  type QueueRun = PresetId -> Task<Result<Preset, ValidationError list>>

[<RequireQualifiedAccess>]
module User =
  type Get = UserId -> Task<User>
  type SetCurrentPreset = UserId -> PresetId -> Task<unit>
  type RemovePreset = UserId -> PresetId -> Task<unit>
  type CreateIfNotExists = UserId -> Task<unit>
  type SetCurrentPresetSize = UserId -> PresetSettings.RawPresetSize -> Task<Result<unit, PresetSettings.Size.ParsingError>>

  type CreatePreset = UserId -> string -> Task<Preset>

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
    | Readable({ Id = id; Name = name }) ->
      { Id = (id |> ReadablePlaylistId)
        Name = name
        Enabled = true
        LikedOnly = false } : IncludedPlaylist
    | Writable({ Id = id; Name = name }) ->
      { Id = (id |> ReadablePlaylistId)
        Name = name
        Enabled = true
        LikedOnly = false }

[<RequireQualifiedAccess>]
module ExcludedPlaylist =
  type Enable = PresetId -> ReadablePlaylistId -> Task<unit>
  type Disable = PresetId -> ReadablePlaylistId -> Task<unit>
  type Remove = PresetId -> ReadablePlaylistId -> Task<unit>

  let fromSpotifyPlaylist =
    function
    | Readable({ Id = id; Name = name }) ->
      { Id = (id |> ReadablePlaylistId)
        Name = name
        Enabled = true }
    | Writable({ Id = id; Name = name }) ->
      { Id = (id |> ReadablePlaylistId)
        Name = name
        Enabled = true }

[<RequireQualifiedAccess>]
module TargetedPlaylist =
  type Enable = PresetId -> TargetedPlaylistId -> Task<unit>
  type Disable = PresetId -> TargetedPlaylistId -> Task<unit>
  type Remove = PresetId -> TargetedPlaylistId -> Task<unit>
  type AppendTracks = PresetId -> TargetedPlaylistId -> Task<unit>
  type OverwriteTracks = PresetId -> TargetedPlaylistId -> Task<unit>

  let fromSpotifyPlaylist =
    function
    | Readable _ -> None
    | Writable({ Id = id; Name = name }) ->
      { Id = (id |> WritablePlaylistId)
        Name = name
        Enabled = true
        Overwrite = false }
      |> Some
