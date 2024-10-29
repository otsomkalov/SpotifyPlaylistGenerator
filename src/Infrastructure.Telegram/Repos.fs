module Infrastructure.Telegram.Repos

open System.Text.RegularExpressions
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums
open Telegram.Bot.Types.ReplyMarkups
open Telegram.Repos
open otsom.fs.Core
open otsom.fs.Extensions

let private escapeMarkdownString (str: string) =
  Regex.Replace(str, "([\(\)`\.#\-!+])", "\$1")

let sendLink (bot: ITelegramBotClient) userId : SendLink =
  fun text linkText link ->
    bot.SendTextMessageAsync(
      (userId |> UserId.value |> ChatId),
      text |> escapeMarkdownString,
      parseMode = ParseMode.MarkdownV2,
      replyMarkup =
        (InlineKeyboardButton(linkText, Url = link)
         |> Seq.singleton
         |> Seq.singleton
         |> InlineKeyboardMarkup)
    )
    |> Task.map ignore