namespace Generator.Bot.Services

open Database
open Domain.Core
open Generator.Bot.Constants
open Infrastructure.Core
open Infrastructure.Workflows
open Resources
open System
open Telegram.Bot.Types.ReplyMarkups

type GetSettingsMessageCommandHandler(_context: AppDbContext) =
  member this.HandleAsync(userId: int64) =
    task {
      let! settings = PresetSettings.load _context (UserId userId)

      let messageText, buttonText, buttonData =
        match settings.LikedTracksHandling with
        | PresetSettings.LikedTracksHandling.Include ->
          Messages.LikedTracksIncluded, Messages.ExcludeLikedTracks, CallbackQueryConstants.excludeLikedTracks
        | PresetSettings.LikedTracksHandling.Exclude ->
          Messages.LikedTracksExcluded, Messages.IgnoreLikedTracks, CallbackQueryConstants.ignoreLikedTracks
        | PresetSettings.LikedTracksHandling.Ignore ->
          Messages.LikedTracksIgnored, Messages.IncludeLikedTracks, CallbackQueryConstants.includeLikedTracks

      let text =
        String.Format(Messages.CurrentSettings, messageText, (settings.PlaylistSize |> PlaylistSize.value))

      let replyMarkup =
        InlineKeyboardMarkup(
          [ InlineKeyboardButton(buttonText, CallbackData = buttonData)
            InlineKeyboardButton(Messages.SetPlaylistSize, CallbackData = CallbackQueryConstants.setPlaylistSize) ]
        )

      return (text, replyMarkup)
    }
