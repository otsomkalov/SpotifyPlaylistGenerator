module Generator.Bot.Services.UnauthorizedUserCommandHandler

open Generator.Bot
open Resources
open Shared
open Telegram.Bot.Types
open Telegram.Bot.Types.ReplyMarkups

let handle (message: Message) env =
  let loginUri =
    ExtendedSpotify.getLoginUrl env

  let replyMarkup =
    InlineKeyboardButton(Messages.Login, Url = loginUri)
    |> InlineKeyboardMarkup

  Bot.sendMessageWithMarkup message.Chat.Id Messages.LoginToSpotify replyMarkup env
