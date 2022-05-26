module Generator.Bot.Services.SettingsCommandHandler

open Shared
open Telegram.Bot.Types

let handle env (message: Message) =
  task {
    let! user = Db.getUser env message.From.Id

    let text, replyMarkup =
      GetSettingsMessageCommandHandler.handle user

    return! Bot.sendMessageWithMarkup env message.Chat.Id text replyMarkup
  }
