module Infrastructure.Core

open Domain.Workflows
open Resources
open System
open Domain.Core

[<RequireQualifiedAccess>]
module TrackId =

  let value (TrackId id) = id

[<RequireQualifiedAccess>]
module RawPlaylistId =
  let value (Playlist.RawPlaylistId rawId) = rawId

module ParsedPlaylistId =
  let value (Playlist.ParsedPlaylistId id) = id

[<RequireQualifiedAccess>]
module PlaylistSize =
  let tryCreate size =
    match size with
    | s when s <= 0 -> Error(Messages.PlaylistSizeTooSmall)
    | s when s >= 10000 -> Error(Messages.PlaylistSizeTooBig)
    | _ -> Ok(PresetSettings.PlaylistSize(size))

  let create size =
    match tryCreate size with
    | Ok size -> size
    | Error e -> ArgumentException(e, nameof size) |> raise

  let value (PresetSettings.PlaylistSize size) = size

[<RequireQualifiedAccess>]
module PresetId =
  let create id =
    match id |> Option.ofNullable with
    | Some id -> PresetId id
    | None -> raise (ArgumentNullException(nameof id, "Given null preset id"))
