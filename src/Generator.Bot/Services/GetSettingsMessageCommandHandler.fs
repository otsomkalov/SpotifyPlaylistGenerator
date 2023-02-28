namespace Generator.Bot.Services

open Database.Entities
open Generator.Bot.Constants
open Resources
open System
open Telegram.Bot.Types.ReplyMarkups

type GetSettingsMessageCommandHandler() =
  member this.HandleAsync(user: User) =
    let messageText, buttonText, buttonData =
      match user.Settings.IncludeLikedTracks |> Option.ofNullable with
      | Some v when v = true -> Messages.LikedTracksIncluded, Messages.ExcludeLikedTracks, CallbackQueryConstants.excludeLikedTracks
      | Some v when v = false -> Messages.LikedTracksExcluded, Messages.IgnoreLikedTracks, CallbackQueryConstants.ignoreLikedTracks
      | None -> Messages.LikedTracksIgnored, Messages.IncludeLikedTracks, CallbackQueryConstants.includeLikedTracks

    let text =
      String.Format(Messages.CurrentSettings, messageText, user.Settings.PlaylistSize)

    let replyMarkup =
      InlineKeyboardMarkup(
        [ InlineKeyboardButton(buttonText, CallbackData = buttonData)
          InlineKeyboardButton(Messages.SetPlaylistSize, CallbackData = CallbackQueryConstants.setPlaylistSize) ]
      )

    (text, replyMarkup)
