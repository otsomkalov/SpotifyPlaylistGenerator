module Shared.Bot

open Microsoft.FSharp.Core
open Telegram.Bot
open Telegram.Bot.Types

[<Interface>]
type IBot =
  abstract Bot: ITelegramBotClient

let sendMessage (env: #IBot) (userId: int64) message =
  task {
    env.Bot.SendTextMessageAsync(ChatId(userId), message)
    |> ignore
  }
