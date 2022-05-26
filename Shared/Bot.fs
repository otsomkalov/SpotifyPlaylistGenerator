module Shared.Bot

open Microsoft.FSharp.Core
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums

[<Interface>]
type IBot =
  abstract Bot: ITelegramBotClient

let sendMessage (env: #IBot) (userId: int64) message =
  task {
    env.Bot.SendTextMessageAsync(ChatId(userId), message)
    |> ignore
  }

let sendMessageWithMarkup (env: #IBot) (userId: int64) message markup =
  task {
    env.Bot.SendTextMessageAsync(ChatId(userId), message, ParseMode.Markdown, replyMarkup = markup)
    |> ignore
  }

let replyToMessage (env: #IBot) (userId: int64) message replyToMessageId =
  task {
    let! _ = env.Bot.SendTextMessageAsync(ChatId(userId), message, replyToMessageId = replyToMessageId)

    return ()
  }

let replyToMessageWithMarkup (env: #IBot) (userId: int64) message replyToMessageId markup =
  task {
    env.Bot.SendTextMessageAsync(ChatId(userId), message, replyToMessageId = replyToMessageId, replyMarkup = markup)
    |> ignore
  }

let answerCallbackQuery (env: #IBot) id =
  task {
    let! _ = env.Bot.AnswerCallbackQueryAsync(id)

    return ()
  }

let answerCallbackQueryWithText (env: #IBot) id text =
  task {
    let! _ = env.Bot.AnswerCallbackQueryAsync(id, text)

    return ()
  }

let editMessageText (env: #IBot) (chatId: int64) messageId text markup =
  task {
    let! _ = env.Bot.EditMessageTextAsync(ChatId(chatId), messageId, text, ParseMode.Markdown, replyMarkup = markup)
    return ()
  }
