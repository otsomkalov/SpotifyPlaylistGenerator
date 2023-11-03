[<RequireQualifiedAccess>]
module Generator.Bot.Telegram

open System.Text.RegularExpressions
open Domain.Workflows
open Shared.Services
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums
open Telegram.Bot.Types.ReplyMarkups
open Telegram.Core
open Telegram.Workflows
open Domain.Extensions

let escapeMarkdownString (str: string) = Regex.Replace(str, "([\(\)`\.#\-!])", "\$1")

let sendMessage (bot: ITelegramBotClient) userId : SendMessage =
  fun text ->
    bot.SendTextMessageAsync(
      (userId |> UserId.value |> ChatId),
      text |> escapeMarkdownString,
      parseMode = ParseMode.MarkdownV2
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
      text |> escapeMarkdownString,
      parseMode = ParseMode.MarkdownV2,
      replyMarkup = replyMarkup
    )
    |> Task.map ignore

let replyToMessage (bot: ITelegramBotClient) userId (messageId: int) : ReplyToMessage =
  fun text ->
    bot.SendTextMessageAsync(
      (userId |> UserId.value |> ChatId),
      text |> escapeMarkdownString,
      parseMode = ParseMode.MarkdownV2,
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
      text |> escapeMarkdownString,
      parseMode = ParseMode.MarkdownV2,
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
      text |> escapeMarkdownString,
      ParseMode.MarkdownV2,
      replyMarkup = replyMarkup
    )
    |> Task.map ignore

let askForReply (bot: ITelegramBotClient) userId messageId : AskForReply =
  fun text ->
    bot.SendTextMessageAsync(
      (userId |> UserId.value |> ChatId),
      text |> escapeMarkdownString,
      parseMode = ParseMode.MarkdownV2,
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
let sendLink (bot: ITelegramBotClient) userId : SendLink =
  fun text linkText link ->
    bot.SendTextMessageAsync(
      (userId |> UserId.value |> ChatId),
      text |> escapeMarkdownString,
      parseMode = ParseMode.MarkdownV2,
      replyMarkup = (InlineKeyboardButton(linkText, Url = link) |> Seq.singleton |> Seq.singleton |> InlineKeyboardMarkup)
    )
    |> Task.map ignore

let checkAuth (spotifyClientProvider: SpotifyClientProvider) : CheckAuth =
  UserId.value
  >> spotifyClientProvider.GetAsync
  >> Task.map (function
    | null -> Unauthorized
    | _ -> Authorized)