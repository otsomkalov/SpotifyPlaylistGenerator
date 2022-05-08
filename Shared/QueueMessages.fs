namespace Shared.QueueMessages

open Microsoft.FSharp.Core
open SpotifyAPI.Web

type IMessage =
  abstract member SpotifyId: string with get, set

type GeneratePlaylistMessage() =
  interface IMessage with
    member val SpotifyId = "" with get, set

  member val TelegramId = 0L with get, set
  member val RefreshCache = false with get, set
  member val SpotifyId = "" with get, set

type SpotifyLoginMessage() =
  interface IMessage with
    member val SpotifyId = "" with get, set

  member val SpotifyId = "" with get, set
  member val TokenResponse: AuthorizationCodeTokenResponse = null with get, set

type LinkAccountsMessage() =
  interface IMessage with
    member val SpotifyId = "" with get, set

  member val SpotifyId = "" with get, set
  member val TelegramId = 0L with get, set

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
