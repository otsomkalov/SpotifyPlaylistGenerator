module Generator.Bot.Services.UnknownCommandHandler

open Shared
open Telegram.Bot.Types

let handle env (message: Message) =
  Bot.replyToMessage env message.Chat.Id "Unknown command" message.MessageId
