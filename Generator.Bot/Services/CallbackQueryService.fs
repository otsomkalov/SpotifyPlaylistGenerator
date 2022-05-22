namespace Generator.Bot.Services

open System.Threading.Tasks
open Generator.Bot.Constants
open Telegram.Bot.Types

type CallbackQueryService(_setIncludeLikedTracksCommandHandler: SetIncludeLikedTracksCommandHandler) =
  member this.HandleAsync(callbackQuery: CallbackQuery) =
    task {
      let processCallbackQueryDataTask =
        match callbackQuery.Data with
        | CallbackQueryConstants.excludeLikedTracks -> _setIncludeLikedTracksCommandHandler.HandleAsync callbackQuery false
        | CallbackQueryConstants.includeLikedTracks -> _setIncludeLikedTracksCommandHandler.HandleAsync callbackQuery true
        | _ -> Task.FromResult()

      return! processCallbackQueryDataTask
    }
