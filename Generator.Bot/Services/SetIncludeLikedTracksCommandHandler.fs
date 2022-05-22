namespace Generator.Bot.Services

open Resources
open Shared.Data
open Telegram.Bot
open Telegram.Bot.Types

type SetIncludeLikedTracksCommandHandler
  (
    _bot: ITelegramBotClient,
    _context: AppDbContext,
    _getSettingsMessageCommandHandler: GetSettingsMessageCommandHandler
  ) =
  member this.HandleAsync(callbackQuery: CallbackQuery) includeLikedTracks =
    task {
      let! user = _context.Users.FindAsync callbackQuery.From.Id

      user.IncludeLikedTracks <- includeLikedTracks

      _context.SaveChangesAsync() |> ignore

      _bot.AnswerCallbackQueryAsync(callbackQuery.Id, Messages.Updated)
      |> ignore

      let text, replyMarkup =
        _getSettingsMessageCommandHandler.HandleAsync(user)

      _bot.EditMessageTextAsync(ChatId(callbackQuery.Message.Chat.Id), callbackQuery.Message.MessageId, text, replyMarkup = replyMarkup)
      |> ignore

      return ()
    }

