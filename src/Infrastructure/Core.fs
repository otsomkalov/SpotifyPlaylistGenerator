module Infrastructure.Core

open Domain.Workflows
open Domain.Core
open MusicPlatform

[<RequireQualifiedAccess>]
module RawPlaylistId =
  let value (Playlist.RawPlaylistId rawId) = rawId