namespace Generator.Bot.Services

open Resources
open System
open Database
open Domain.Core
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums

type SetIncludeLikedTracksCommandHandler
  (
    _bot: ITelegramBotClient,
    _context: AppDbContext,
    _getSettingsMessageCommandHandler: GetSettingsMessageCommandHandler
  ) =
  member this.HandleAsync likedTracksHandling (callbackQuery: CallbackQuery) =
    task {
      let! user = _context.Users.FindAsync callbackQuery.From.Id

      user.Settings.IncludeLikedTracks <-
        (match likedTracksHandling with
         | LikedTracksHandling.Include -> Nullable true
         | LikedTracksHandling.Exclude -> Nullable false
         | LikedTracksHandling.Ignore -> Nullable<bool>())

      let! _ = _context.SaveChangesAsync()

      _bot.AnswerCallbackQueryAsync(callbackQuery.Id, Messages.Updated) |> ignore

      let text, replyMarkup = _getSettingsMessageCommandHandler.HandleAsync(user)

      _bot.EditMessageTextAsync(
        ChatId(callbackQuery.Message.Chat.Id),
        callbackQuery.Message.MessageId,
        text,
        ParseMode.Markdown,
        replyMarkup = replyMarkup
      )
      |> ignore

      return ()
    }
