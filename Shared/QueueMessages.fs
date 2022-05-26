namespace Shared.QueueMessages

open Microsoft.FSharp.Core

type GeneratePlaylistMessage =
  { TelegramId: int64
    RefreshCache: bool }

module MessageTypes =
  [<Literal>]
  let GeneratePlaylist = "GeneratePlaylist"
