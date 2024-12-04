namespace MusicPlatform

open System.Threading.Tasks

type PlaylistId = PlaylistId of string
type TrackId = TrackId of string

type ArtistId = ArtistId of string

type Artist = { Id: ArtistId }

type Track = { Id: TrackId; Artists: Set<Artist> }

[<RequireQualifiedAccess>]
module TrackId =
  let value (TrackId id) = id

type PlaylistData = { Id: PlaylistId; Name: string }

type Playlist =
  | Readable of PlaylistData
  | Writable of PlaylistData

[<RequireQualifiedAccess>]
module Playlist =
  type ListTracks = PlaylistId -> Task<Track list>
  type AddTracks = PlaylistId -> Track list -> Task<unit>
  type ReplaceTracks = PlaylistId -> Track list -> Task<unit>

  type LoadError = | NotFound

  type Load = PlaylistId -> Task<Result<Playlist, LoadError>>

  type RawPlaylistId = RawPlaylistId of string
  type IdParsingError = IdParsingError of string

  type ParseId = RawPlaylistId -> Result<PlaylistId, IdParsingError>

[<RequireQualifiedAccess>]
module User =
  type ListLikedTracks = unit -> Task<Track list>

[<RequireQualifiedAccess>]
module Track =
  type GetRecommendations = TrackId list -> Task<Track list>