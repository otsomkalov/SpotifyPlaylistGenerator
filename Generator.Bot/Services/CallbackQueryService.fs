module Generator.Bot.Services.CallbackQueryService

open Generator.Bot.Constants
open Telegram.Bot.Types

let handle (callbackQuery: CallbackQuery) env =
  task {
    let processCallbackQueryDataTask =
      match callbackQuery.Data with
      | CallbackQueryConstants.excludeLikedTracks -> SetIncludeLikedTracksCommandHandler.handle false
      | CallbackQueryConstants.includeLikedTracks -> SetIncludeLikedTracksCommandHandler.handle true
      | CallbackQueryConstants.setPlaylistSize -> SetPlaylistSizeCommandHandler.handleCallbackQuery

    return! processCallbackQueryDataTask env callbackQuery
  }
