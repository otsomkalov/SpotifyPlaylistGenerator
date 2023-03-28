module Infrastructure.Core

open Resources
open System
open Domain.Core

[<RequireQualifiedAccess>]
module UserId =

  let value (UserId id) = id

[<RequireQualifiedAccess>]
module TrackId =

  let value (TrackId id) = id

[<RequireQualifiedAccess>]
module PlaylistSize =
  let tryCreate size =
    match size with
    | s when s <= 0 -> Error(Messages.PlaylistSizeTooSmall)
    | s when s >= 10000 -> Error(Messages.PlaylistSizeTooBig)
    | _ -> Ok(UserSettings.PlaylistSize(size))

  let create size =
    match tryCreate size with
    | Ok size -> size
    | Error e -> ArgumentException(e, nameof size) |> raise

  let value (UserSettings.PlaylistSize size) = size

[<RequireQualifiedAccess>]
module ReadablePlaylistId =
  let value (ReadablePlaylistId id) = id