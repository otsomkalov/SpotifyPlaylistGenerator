module Generator.Bot.Services.UnknownCommandHandler

open Shared
open Telegram.Bot.Types

let handle (message: Message) env =
  Bot.replyToMessage message.Chat.Id "Unknown command" message.MessageId env
