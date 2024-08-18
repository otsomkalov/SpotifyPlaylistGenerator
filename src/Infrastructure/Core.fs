module Infrastructure.Core

open Domain.Workflows
open Domain.Core

[<RequireQualifiedAccess>]
module RawPlaylistId =
  let value (Playlist.RawPlaylistId rawId) = rawId