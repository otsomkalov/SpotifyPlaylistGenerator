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

  let appendToTargetPlaylist userId playlistId (callbackQuery: CallbackQuery) =
    task {
      do! Infrastructure.Workflows.TargetPlaylist.appendToTargetPlaylist _context userId playlistId

      let! _ = _bot.AnswerCallbackQueryAsync(callbackQuery.Id, "Target playlist will be appended with generated tracks")

      return ()
    }

  let overwriteTargetPlaylist userId playlistId (callbackQuery: CallbackQuery) =
    task {
      do! Infrastructure.Workflows.TargetPlaylist.overwriteTargetPlaylist _context userId playlistId

      let! _ = _bot.AnswerCallbackQueryAsync(callbackQuery.Id, "Target playlist will be overwritten with generated tracks")

      return ()
    }

  member this.ProcessAsync(callbackQuery: CallbackQuery) =
    task {
      let processCallbackQueryDataTask: CallbackQuery -> Task<unit> =
        let userId = callbackQuery.From.Id |> UserId

        match callbackQuery.Data with
        | CallbackQueryConstants.includeLikedTracks ->
          _setIncludeLikedTracksCommandHandler.HandleAsync PresetSettings.LikedTracksHandling.Include
        | CallbackQueryConstants.excludeLikedTracks ->
          _setIncludeLikedTracksCommandHandler.HandleAsync PresetSettings.LikedTracksHandling.Exclude
        | CallbackQueryConstants.ignoreLikedTracks ->
          _setIncludeLikedTracksCommandHandler.HandleAsync PresetSettings.LikedTracksHandling.Ignore
        | CallbackQueryConstants.setPlaylistSize -> _setPlaylistSizeCommandHandler.HandleAsync
        | CallbackQueryData("tp", id, "a") -> appendToTargetPlaylist userId (id |> PlaylistId |> WritablePlaylistId)
        | CallbackQueryData("tp", id, "o") -> overwriteTargetPlaylist userId (id |> PlaylistId |> WritablePlaylistId)

      return! processCallbackQueryDataTask callbackQuery
    }
