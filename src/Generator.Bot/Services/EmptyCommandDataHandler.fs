namespace Generator.Bot.Services

open Telegram.Bot
open Telegram.Bot.Types

type EmptyCommandDataHandler(_bot: ITelegramBotClient) =
  member this.HandleAsync(message: Message) =
    task {
      _bot.SendTextMessageAsync(ChatId(message.Chat.Id), "You have entered empty playlist url", replyToMessageId = message.MessageId)
      |> ignore
    }
