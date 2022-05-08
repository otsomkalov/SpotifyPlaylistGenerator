namespace Bot.Services.Bot

open Telegram.Bot
open Telegram.Bot.Types

type UnknownCommandHandler(_bot: ITelegramBotClient) =
  member this.HandleAsync(message: Message) =
    task {
      let! _ =
        (ChatId(message.From.Id), "Unknown command")
        |> _bot.SendTextMessageAsync

      return ()
    }
