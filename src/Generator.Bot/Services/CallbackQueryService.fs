namespace Generator.Bot.Services

open System.Threading.Tasks
open Database
open Domain.Core
open Generator.Bot.Constants
open Telegram.Bot
open Telegram.Bot.Types
open Generator.Bot.Helpers

type CallbackQueryService
  (
    _setIncludeLikedTracksCommandHandler: SetIncludeLikedTracksCommandHandler,
    _setPlaylistSizeCommandHandler: SetPlaylistSizeCommandHandler,
    _context: AppDbContext,
    _bot: ITelegramBotClient
  ) =

  let appendToTargetPlaylist playlistId (callbackQuery: CallbackQuery) =
    task{
      do! Infrastructure.Workflows.TargetPlaylist.appendToTargetPlaylist _context playlistId

      let! _ = _bot.AnswerCallbackQueryAsync(callbackQuery.Id, "Target playlist will be appended with generated tracks")

      return ()
    }

  let overwriteTargetPlaylist playlistId (callbackQuery: CallbackQuery) =
    task{
      do! Infrastructure.Workflows.TargetPlaylist.overwriteTargetPlaylist _context playlistId

      let! _ = _bot.AnswerCallbackQueryAsync(callbackQuery.Id, "Target playlist will be appended with generated tracks")

      return ()
    }

  member this.ProcessAsync(callbackQuery: CallbackQuery) =
    task {
      let processCallbackQueryDataTask: CallbackQuery -> Task<unit> =
        match callbackQuery.Data with
        | CallbackQueryConstants.includeLikedTracks ->
          _setIncludeLikedTracksCommandHandler.HandleAsync UserSettings.LikedTracksHandling.Include
        | CallbackQueryConstants.excludeLikedTracks ->
          _setIncludeLikedTracksCommandHandler.HandleAsync UserSettings.LikedTracksHandling.Exclude
        | CallbackQueryConstants.ignoreLikedTracks ->
          _setIncludeLikedTracksCommandHandler.HandleAsync UserSettings.LikedTracksHandling.Ignore
        | CallbackQueryConstants.setPlaylistSize -> _setPlaylistSizeCommandHandler.HandleAsync
        | CallbackQueryData("tp", id, "a") -> appendToTargetPlaylist id
        | CallbackQueryData("tp", id, "o") -> overwriteTargetPlaylist id

      return! processCallbackQueryDataTask callbackQuery
    }
