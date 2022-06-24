module Generator.Bot.Services.CallbackQueryService

open System.Threading.Tasks
open Generator.Bot
open Generator.Bot.Constants
open Generator.Bot.Env
open Generator.Bot.Services
open Telegram.Bot.Types

let handle (callbackQuery: CallbackQuery) env : Task<unit> =
  let getProcessCallbackQueryFuncTask =
    match callbackQuery.Data with
    | CallbackQueryConstants.excludeLikedTracks -> SetIncludeLikedTracksCommandHandler.handle false
    | CallbackQueryConstants.includeLikedTracks -> SetIncludeLikedTracksCommandHandler.handle true
    | CallbackQueryConstants.setPlaylistSize -> SetPlaylistSizeCommandHandler.handleCallbackQuery

  getProcessCallbackQueryFuncTask callbackQuery env
