module Generator.Bot.Services.UnauthorizedUserCommandHandler

open Generator.Bot
open Resources
open Shared
open Telegram.Bot.Types
open Telegram.Bot.Types.ReplyMarkups

let handle env (message: Message) =
  task {
    let loginUri = ExtendedSpotify.getLoginUrl env

    let replyMarkup =
      InlineKeyboardButton(Messages.Login, Url = loginUri)
      |> InlineKeyboardMarkup

    return! Bot.sendMessageWithMarkup env message.Chat.Id Messages.LoginToSpotify replyMarkup
  }
