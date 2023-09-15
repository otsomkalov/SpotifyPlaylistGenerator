module Telegram.Constants

open Microsoft.FSharp.Core

module CallbackQueryConstants =
  [<Literal>]
  let includeLikedTracks = "ilt"
  [<Literal>]
  let excludeLikedTracks = "elt"
  [<Literal>]
  let ignoreLikedTracks = "ignore-liked-tracks"
  [<Literal>]
  let setPlaylistSize = "sps"