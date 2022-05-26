module Generator.Bot.Services.SetIncludeLikedTracksCommandHandler

open Resources
open Shared
open Telegram.Bot.Types

let handle includeLikedTracks env (callbackQuery: CallbackQuery) =
  task {
    let! user = Db.getUser env callbackQuery.From.Id

    user.Settings.IncludeLikedTracks <- includeLikedTracks

    do! Db.updateUser env user

    do! Bot.answerCallbackQueryWithText env callbackQuery.Id Messages.Updated

    let text, replyMarkup =
      GetSettingsMessageCommandHandler.handle user

    do! Bot.editMessageText env callbackQuery.Message.Chat.Id callbackQuery.Message.MessageId text replyMarkup

    return ()
  }
