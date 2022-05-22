namespace Generator.Bot.Services

open Generator.Bot.Constants
open Telegram.Bot.Types

type CallbackQueryService(_setIncludeLikedTracksCommandHandler: SetIncludeLikedTracksCommandHandler) =
  member this.HandleAsync(callbackQuery: CallbackQuery) =
    task {
      let processCallbackQueryDataTask =
        match callbackQuery.Data with
        | CallbackQueryConstants.excludeLikedTracks -> _setIncludeLikedTracksCommandHandler.HandleAsync false
        | CallbackQueryConstants.includeLikedTracks -> _setIncludeLikedTracksCommandHandler.HandleAsync true

      return! processCallbackQueryDataTask callbackQuery
    }
