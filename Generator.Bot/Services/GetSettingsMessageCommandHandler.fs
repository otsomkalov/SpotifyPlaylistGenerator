namespace Generator.Bot.Services

open Data.Entities
open Generator.Bot.Constants
open Resources
open System
open Telegram.Bot.Types.ReplyMarkups

type GetSettingsMessageCommandHandler() =
  member this.HandleAsync(user: User) =
    let includeLikedTracksMark, buttonText, callbackData =
      if user.IncludeLikedTracks then
        ("✅", Messages.ExcludeLikedTracks, CallbackQueryConstants.excludeLikedTracks)
      else
        ("❌", Messages.IncludeLikedTracks, CallbackQueryConstants.includeLikedTracks)

    let text =
      String.Format(Messages.CurrentSettings, includeLikedTracksMark)

    let replyMarkup =
      InlineKeyboardMarkup([ InlineKeyboardButton(buttonText, CallbackData = callbackData) ])

    (text, replyMarkup)
