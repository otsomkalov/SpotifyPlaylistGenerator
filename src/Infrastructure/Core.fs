module Infrastructure.Core

open Domain.Workflows
open Domain.Core

[<RequireQualifiedAccess>]
module TrackId =

  let value (TrackId id) = id

[<RequireQualifiedAccess>]
module RawPlaylistId =
  let value (Playlist.RawPlaylistId rawId) = rawId

module ParsedPlaylistId =
  let value (Playlist.ParsedPlaylistId id) = id