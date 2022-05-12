namespace Shared.QueueMessages

open Microsoft.FSharp.Core

type GeneratePlaylistMessage =
  { TelegramId: int64
    RefreshCache: bool }

module MessageTypes =
  [<Literal>]
  let SpotifyLogin = "SpotifyLogin"

  [<Literal>]
  let LinkAccounts = "LinkAccounts"

  [<Literal>]
  let GeneratePlaylist = "GeneratePlaylist"

module MessageAttributeNames =
  [<Literal>]
  let Type = "Type"
