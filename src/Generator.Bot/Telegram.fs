﻿[<RequireQualifiedAccess>]
module Generator.Bot.Telegram

open Domain.Workflows
open Infrastructure.Helpers
open Shared.Services
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums
open Telegram.Bot.Types.ReplyMarkups
open Telegram.Core
open Telegram.Workflows

let sendMessage (bot: ITelegramBotClient) userId : SendMessage =
  fun text ->
    bot.SendTextMessageAsync(
      (userId |> UserId.value |> ChatId),
      text,
      ParseMode.MarkdownV2
    )
    |> Task.map ignore
let sendButtons (bot: ITelegramBotClient) userId : SendButtons =
  fun text buttons ->
    let replyMarkup =
      buttons
      |> Seq.map(Seq.map(InlineKeyboardButton.WithCallbackData))
      |> InlineKeyboardMarkup

    bot.SendTextMessageAsync(
      (userId |> UserId.value |> ChatId),
      text,
      ParseMode.MarkdownV2,
      replyMarkup = replyMarkup
    )
    |> Task.map ignore

let replyToMessage (bot: ITelegramBotClient) userId (messageId: int) : ReplyToMessage =
  fun text ->
    bot.SendTextMessageAsync(
      (userId |> UserId.value |> ChatId),
      text,
      ParseMode.MarkdownV2,
      replyToMessageId = messageId
    )
    |> Task.map ignore

let sendKeyboard (bot: ITelegramBotClient) userId : SendKeyboard =
  fun text buttons ->
    let replyMarkup =
      buttons
      |> Seq.map Seq.toArray
      |> Seq.toArray
      |> ReplyKeyboardMarkup.op_Implicit

    bot.SendTextMessageAsync(
      (userId |> UserId.value |> ChatId),
      text,
      ParseMode.MarkdownV2,
      replyMarkup = replyMarkup
    )
    |> Task.map ignore

let editMessage (bot: ITelegramBotClient) messageId userId: EditMessage =
  fun text buttons ->
    let replyMarkup =
      buttons
      |> Seq.map(Seq.map(InlineKeyboardButton.WithCallbackData))
      |> InlineKeyboardMarkup

    bot.EditMessageTextAsync(
      (userId |> UserId.value |> ChatId),
      messageId,
      text,
      ParseMode.MarkdownV2,
      replyMarkup = replyMarkup
    )
    |> Task.map ignore

let askForReply (bot: ITelegramBotClient) userId messageId : AskForReply =
  fun text ->
    bot.SendTextMessageAsync(
      (userId |> UserId.value |> ChatId),
      text,
      ParseMode.MarkdownV2,
      replyToMessageId = messageId,
      replyMarkup = ForceReplyMarkup()
    )
    |> Task.map ignore

let answerCallbackQuery (bot: ITelegramBotClient) callbackQueryId : AnswerCallbackQuery =
  fun text ->
    task {
      do! bot.AnswerCallbackQueryAsync(callbackQueryId, text)

      return ()
    }

let checkAuth (spotifyClientProvider: SpotifyClientProvider) : CheckAuth =
  UserId.value
  >> spotifyClientProvider.GetAsync
  >> Task.map (function
    | null -> Unauthorized
    | _ -> Authorized)