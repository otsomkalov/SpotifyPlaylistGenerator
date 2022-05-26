module Generator.Bot.Services.UnauthorizedUserCommandHandler

open System.Collections.Generic
open Resources
open Shared
open SpotifyAPI.Web
open Telegram.Bot.Types
open Telegram.Bot.Types.ReplyMarkups

let handle env (message: Message) =
  task {
    let scopes =
      [ Scopes.PlaylistModifyPrivate
        Scopes.PlaylistModifyPublic
        Scopes.UserLibraryRead ]
      |> List<string>

    let loginRequest =
      LoginRequest(SpotifySettings.callbackUrl env, SpotifySettings.clientUrl env, LoginRequest.ResponseType.Code, Scope = scopes)

    let replyMarkup =
      InlineKeyboardButton(Messages.Login, Url = loginRequest.ToUri().ToString())
      |> InlineKeyboardMarkup

    return! Bot.sendMessageWithMarkup env message.Chat.Id Messages.LoginToSpotify replyMarkup
  }
