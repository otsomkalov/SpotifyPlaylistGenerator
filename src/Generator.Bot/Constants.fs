module Generator.Bot.Constants

open Microsoft.FSharp.Core

module CallbackQueryConstants =
  [<Literal>]
  let includeLikedTracks = "ilt"
  [<Literal>]
  let excludeLikedTracks = "elt"
  [<Literal>]
  let setPlaylistSize = "sps"