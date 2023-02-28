namespace Generator.Bot.Services

open System.Threading.Tasks
open Database.Migrations
open Domain.Core
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
        | CallbackQueryConstants.includeLikedTracks -> _setIncludeLikedTracksCommandHandler.HandleAsync LikedTracksHandling.Include
        | CallbackQueryConstants.excludeLikedTracks -> _setIncludeLikedTracksCommandHandler.HandleAsync LikedTracksHandling.Exclude
        | CallbackQueryConstants.ignoreLikedTracks -> _setIncludeLikedTracksCommandHandler.HandleAsync LikedTracksHandling.Ignore
        | CallbackQueryConstants.setPlaylistSize -> _setPlaylistSizeCommandHandler.HandleAsync

      return! processCallbackQueryDataTask callbackQuery
    }
