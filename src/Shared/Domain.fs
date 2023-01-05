module Shared.Domain

open Resources

module PlaylistSize =
  type PlaylistSize = private PlaylistSize of int

  let create size =
    match size with
    | s when s <= 0 -> Error(Messages.PlaylistSizeTooSmall)
    | s when s >= 10000 -> Error(Messages.PlaylistSizeTooBig)
    | _ -> Ok(PlaylistSize(size))

  let value (PlaylistSize size) = size
