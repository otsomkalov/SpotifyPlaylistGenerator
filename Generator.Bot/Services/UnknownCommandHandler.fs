namespace Generator.Bot.Services

open Telegram.Bot
open Telegram.Bot.Types

type UnknownCommandHandler(_bot: ITelegramBotClient) =
  member this.HandleAsync(message: Message) =
    task {
      _bot.SendTextMessageAsync(ChatId(message.Chat.Id), "Unknown command", replyToMessageId = message.MessageId)
      |> ignore
    }
