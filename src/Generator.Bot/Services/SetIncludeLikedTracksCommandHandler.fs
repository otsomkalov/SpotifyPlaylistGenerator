namespace Generator.Bot.Services

open Database
open Resources
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums

type SetIncludeLikedTracksCommandHandler
  (
    _bot: ITelegramBotClient,
    _context: AppDbContext,
    _getSettingsMessageCommandHandler: GetSettingsMessageCommandHandler
  ) =
  member this.HandleAsync includeLikedTracks (callbackQuery: CallbackQuery) =
    task {
      let! user = _context.Users.FindAsync callbackQuery.From.Id

      user.Settings.IncludeLikedTracks <- includeLikedTracks

      let! _ = _context.SaveChangesAsync()

      _bot.AnswerCallbackQueryAsync(callbackQuery.Id, Messages.Updated)
      |> ignore

      let text, replyMarkup =
        _getSettingsMessageCommandHandler.HandleAsync(user)

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
