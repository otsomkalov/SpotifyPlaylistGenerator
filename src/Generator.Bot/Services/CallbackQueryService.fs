namespace Generator.Bot.Services

open System.Threading.Tasks
open Generator.Bot.Constants
open Telegram.Bot.Types

type CallbackQueryService
  (
    _setIncludeLikedTracksCommandHandler: SetIncludeLikedTracksCommandHandler,
    _setPlaylistSizeCommandHandler: SetPlaylistSizeCommandHandler
  ) =
  member this.ProcessAsync(callbackQuery: CallbackQuery) =
    task {
      let processCallbackQueryDataTask : CallbackQuery -> Task<unit> =
        match callbackQuery.Data with
        | CallbackQueryConstants.excludeLikedTracks -> _setIncludeLikedTracksCommandHandler.HandleAsync false
        | CallbackQueryConstants.includeLikedTracks -> _setIncludeLikedTracksCommandHandler.HandleAsync true
        | CallbackQueryConstants.setPlaylistSize -> _setPlaylistSizeCommandHandler.HandleAsync

      return! processCallbackQueryDataTask callbackQuery
    }
