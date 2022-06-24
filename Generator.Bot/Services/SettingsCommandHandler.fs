module Generator.Bot.Services.SettingsCommandHandler

open Shared
open Telegram.Bot.Types

let handle (message: Message) env =
  task {
    let! user = Db.getUser env message.From.Id

    let text, replyMarkup =
      GetSettingsMessageCommandHandler.handle user

    return! Bot.sendMessageWithMarkup message.Chat.Id text replyMarkup env
  }
