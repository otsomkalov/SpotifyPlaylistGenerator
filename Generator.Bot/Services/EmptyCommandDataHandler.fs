module Generator.Bot.Services.EmptyCommandDataHandler

open Shared
open Telegram.Bot.Types

let handle env (message: Message) =
  Bot.replyToMessage env message.Chat.Id "You have entered empty playlist url" message.MessageId
