namespace Generator.Bot.Services

open Telegram.Bot
open Telegram.Bot.Types

type UnknownCommandHandler(_bot: ITelegramBotClient) =
  member this.HandleAsync replyToMessage (message: Message) =
    replyToMessage "Unknown command"
