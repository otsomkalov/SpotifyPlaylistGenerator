namespace Generator.Bot.Services

open System.Collections.Generic
open Microsoft.Extensions.Options
open Shared.Settings
open SpotifyAPI.Web
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.ReplyMarkups

type UnauthorizedUserCommandHandler(_bot: ITelegramBotClient, _spotifyOptions: IOptions<SpotifySettings>) =
  let _spotifySettings = _spotifyOptions.Value

  member this.HandleAsync(message: Message) =
    task {
      let scopes =
        [ Scopes.PlaylistModifyPrivate
          Scopes.PlaylistModifyPublic
          Scopes.UserLibraryRead ]
        |> List<string>

      let loginRequest =
        LoginRequest(_spotifySettings.CallbackUrl, _spotifySettings.ClientId, LoginRequest.ResponseType.Code, Scope = scopes)

      let replyMarkup =
        InlineKeyboardButton("Login", Url = loginRequest.ToUri().ToString())
        |> InlineKeyboardMarkup

      let! _ = _bot.SendTextMessageAsync(ChatId(message.From.Id), "Login to generate playlist", replyMarkup = replyMarkup)

      ()
    }
