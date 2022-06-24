module Generator.Bot.Services.SetIncludeLikedTracksCommandHandler

open Resources
open Shared
open Telegram.Bot.Types

let handle includeLikedTracks (callbackQuery: CallbackQuery) env =
  task {
    let! user = Db.getUser env callbackQuery.From.Id

    user.Settings.IncludeLikedTracks <- includeLikedTracks

    do! Db.updateUser env user

    do! Bot.answerCallbackQueryWithText env callbackQuery.Id Messages.Updated

    let text, replyMarkup =
      GetSettingsMessageCommandHandler.handle user

    return! Bot.editMessageText callbackQuery.Message.Chat.Id callbackQuery.Message.MessageId text replyMarkup env
  }
