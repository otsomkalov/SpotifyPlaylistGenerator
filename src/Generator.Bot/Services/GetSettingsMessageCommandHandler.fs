namespace Generator.Bot.Services

open Database
open Domain.Core
open Domain.Workflows
open Generator.Bot.Constants
open Infrastructure.Core
open Infrastructure.Helpers
open Infrastructure.Workflows
open Resources
open System
open Microsoft.EntityFrameworkCore
open Telegram.Bot.Types.ReplyMarkups
open System.Linq

type GetSettingsMessageCommandHandler(_context: AppDbContext) =
  member this.HandleAsync(userId: int64) =
    task {
      let! settings = PresetSettings.load _context (UserId userId)
      let! presetId =
        _context.Users.AsNoTracking().Where(fun u -> u.Id = userId).Select(fun u -> u.CurrentPresetId).FirstOrDefaultAsync()
        |> Task.map (fun p -> p.Value)

      let messageText, buttonText, buttonData =
        match settings.LikedTracksHandling with
        | PresetSettings.LikedTracksHandling.Include ->
          Messages.LikedTracksIncluded, Messages.ExcludeLikedTracks, $"p|{presetId}|{CallbackQueryConstants.excludeLikedTracks}"
        | PresetSettings.LikedTracksHandling.Exclude ->
          Messages.LikedTracksExcluded, Messages.IgnoreLikedTracks, $"p|{presetId}|{CallbackQueryConstants.ignoreLikedTracks}"
        | PresetSettings.LikedTracksHandling.Ignore ->
          Messages.LikedTracksIgnored, Messages.IncludeLikedTracks, $"p|{presetId}|{CallbackQueryConstants.includeLikedTracks}"

      let text =
        String.Format(Messages.CurrentSettings, messageText, (settings.PlaylistSize |> PlaylistSize.value))

      let replyMarkup =
        InlineKeyboardMarkup(
          [ InlineKeyboardButton(buttonText, CallbackData = buttonData)
            InlineKeyboardButton(Messages.SetPlaylistSize, CallbackData = CallbackQueryConstants.setPlaylistSize) ]
        )

      return (text, replyMarkup)
    }
