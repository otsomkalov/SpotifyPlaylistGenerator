namespace Shared.QueueMessages

open System.Text.Json.Serialization
open Microsoft.FSharp.Core
open SpotifyAPI.Web

type GeneratePlaylistMessage =
  { TelegramId: int64
    RefreshCache: bool
    SpotifyId: string }

type SpotifyLoginMessage =
  { SpotifyId: string
    TokenResponse: AuthorizationCodeTokenResponse }

type LinkAccountsMessage =
  { SpotifyId: string
    TelegramId: int64 }

[<JsonFSharpConverter(unionEncoding = (JsonUnionEncoding.Untagged ||| JsonUnionEncoding.UnwrapRecordCases))>]
type QueueMessage =
  | GeneratePlaylist of GeneratePlaylistMessage
  | SpotifyLogin of SpotifyLoginMessage
  | LinkAccounts of LinkAccountsMessage
  member this.SpotifyId =
    match this with
    | GeneratePlaylist m -> m.SpotifyId
    | LinkAccounts m -> m.SpotifyId
    | SpotifyLogin m -> m.SpotifyId

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
