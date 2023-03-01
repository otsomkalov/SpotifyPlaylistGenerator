namespace Generator.Bot.Services

open Resources
open Database
open Domain.Core
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums

type SetIncludeLikedTracksCommandHandler
  (
    _bot: ITelegramBotClient,
    _context: AppDbContext,
    _getSettingsMessageCommandHandler: GetSettingsMessageCommandHandler,
    setLikedTracksHandling: UserSettings.SetLikedTracksHandling
  ) =
  member this.HandleAsync likedTracksHandling (callbackQuery: CallbackQuery) =
    let setLikedTracksHandling =
      setLikedTracksHandling (callbackQuery.From.Id |> UserId)

    task {
      do! setLikedTracksHandling likedTracksHandling

      _bot.AnswerCallbackQueryAsync(callbackQuery.Id, Messages.Updated) |> ignore

      let! text, replyMarkup = _getSettingsMessageCommandHandler.HandleAsync(callbackQuery.From.Id)

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
