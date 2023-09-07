namespace Generator.Bot.Services

open Azure.Storage.Queues
open Database
open Domain
open Domain.Core
open Generator.Bot
open Infrastructure.Workflows
open Telegram.Bot
open Telegram.Bot.Types

[<NoEquality; NoComparison>]
type ProcessCallbackQueryDeps ={
  LoadPreset: Workflows.Preset.Load
  AskForPlaylistSize: Telegram.AskForPlaylistSize
}

type CallbackQueryService
  (
    _setPlaylistSizeCommandHandler: SetPlaylistSizeCommandHandler,
    _context: AppDbContext,
    _bot: ITelegramBotClient,
    _queueClient: QueueClient,
    deps: ProcessCallbackQueryDeps,
    getPresetMessage: Telegram.GetPresetMessage
  ) =

  let appendToTargetPlaylistBuilder callbackQueryId presetId playlistId =
    task {
      do! TargetPlaylist.appendToTargetPlaylist _context presetId playlistId

      let! _ = _bot.AnswerCallbackQueryAsync(callbackQueryId, "Target playlist will be appended with generated tracks")

      return ()
    }

  let overwriteTargetPlaylistBuilder callbackQueryId presetId playlistId =
    task {
      do! TargetPlaylist.overwriteTargetPlaylist _context presetId playlistId

      let! _ = _bot.AnswerCallbackQueryAsync(callbackQueryId, "Target playlist will be overwritten with generated tracks")

      return ()
    }

  let showIncludedPlaylistBuilder callbackQueryId presetId playlistId =
    task{
      do! _bot.AnswerCallbackQueryAsync(callbackQueryId, "Not implemented yet")

      return ()
    }

  let removeIncludedPlaylistBuilder callbackQueryId presetId playlistId =
    task{
      do! _bot.AnswerCallbackQueryAsync(callbackQueryId, "Not implemented yet")

      return ()
    }

  let removeExcludedPlaylistBuilder callbackQueryId presetId playlistId =
      task{
        do! _bot.AnswerCallbackQueryAsync(callbackQueryId, "Not implemented yet")

        return ()
      }

  let removeTargetPlaylistBuilder callbackQueryId presetId playlistId =
      task{
        do! _bot.AnswerCallbackQueryAsync(callbackQueryId, "Not implemented yet")

        return ()
      }

  let showExcludedPlaylistBuilder callbackQueryId presetId playlistId =
    task{
      do! _bot.AnswerCallbackQueryAsync(callbackQueryId, "Not implemented yet")

      return ()
    }

  let showTargetPlaylistBuilder callbackQueryId presetId playlistId =
    task{
      do! _bot.AnswerCallbackQueryAsync(callbackQueryId, "Not implemented yet")

      return ()
    }

  let setLikedTracksHandlingBuilder callbackQueryId messageId userId presetId handling  =
    let updatePresetSettings = Preset.updateSettings _context presetId
    let setLikedTracksHandling = Preset.setLikedTracksHandling deps.LoadPreset updatePresetSettings
    let sendPresetInfo = Telegram.sendPresetInfo _bot getPresetMessage messageId userId

    let setLikedTracksHandling = Telegram.setLikedTracksHandling _bot setLikedTracksHandling sendPresetInfo callbackQueryId

    setLikedTracksHandling presetId handling

  member this.ProcessAsync(callbackQuery: CallbackQuery) =
    let userId = callbackQuery.From.Id |> UserId

    let editMessage = Telegram.editMessage _bot callbackQuery.Message.MessageId userId

    let sendPresetInfo = Telegram.sendPresetInfo _bot getPresetMessage callbackQuery.Message.MessageId userId
    let setCurrentPreset = Telegram.setCurrentPreset _bot _context callbackQuery.Id

    let showIncludedPlaylists = Telegram.showIncludedPlaylists deps.LoadPreset editMessage
    let showExcludedPlaylists = Telegram.showExcludedPlaylists deps.LoadPreset editMessage
    let showTargetPlaylists = Telegram.showTargetPlaylists deps.LoadPreset editMessage

    let showIncludedPlaylist = showIncludedPlaylistBuilder callbackQuery.Id
    let showExcludedPlaylist = showExcludedPlaylistBuilder callbackQuery.Id
    let showTargetPlaylist = showTargetPlaylistBuilder callbackQuery.Id

    let removeIncludedPlaylist = removeIncludedPlaylistBuilder callbackQuery.Id
    let removeExcludedPlaylist = removeExcludedPlaylistBuilder callbackQuery.Id
    let removeTargetPlaylist = removeTargetPlaylistBuilder callbackQuery.Id

    let appendToTargetPlaylist = appendToTargetPlaylistBuilder callbackQuery.Id
    let overwriteTargetPlaylist = overwriteTargetPlaylistBuilder callbackQuery.Id

    let setLikedTracksHandling = setLikedTracksHandlingBuilder callbackQuery.Id callbackQuery.Message.MessageId userId

    match callbackQuery.Data |> Telegram.parseAction with
    | Telegram.Action.ShowPresetInfo presetId -> sendPresetInfo presetId
    | Telegram.Action.SetCurrentPreset presetId -> setCurrentPreset userId presetId

    | Telegram.Action.ShowIncludedPlaylists(presetId, page) -> showIncludedPlaylists presetId page
    | Telegram.Action.ShowExcludedPlaylists(presetId, page) -> showExcludedPlaylists presetId page
    | Telegram.Action.ShowTargetPlaylists(presetId, page) -> showTargetPlaylists presetId page

    | Telegram.Action.ShowIncludedPlaylist(presetId, playlistId) -> showIncludedPlaylist presetId playlistId
    | Telegram.Action.ShowExcludedPlaylist(presetId, playlistId) -> showExcludedPlaylist presetId playlistId
    | Telegram.Action.ShowTargetPlaylist(presetId, playlistId) -> showTargetPlaylist presetId playlistId

    | Telegram.Action.RemoveIncludedPlaylist(presetId, playlistId) -> removeIncludedPlaylist presetId playlistId
    | Telegram.Action.RemoveExcludedPlaylist(presetId, playlistId) -> removeExcludedPlaylist presetId playlistId
    | Telegram.Action.RemoveTargetPlaylist(presetId, playlistId) -> removeTargetPlaylist presetId playlistId

    | Telegram.Action.AppendToTargetPlaylist(presetId, playlistId) -> appendToTargetPlaylist presetId playlistId
    | Telegram.Action.OverwriteTargetPlaylist(presetId, playlistId) -> overwriteTargetPlaylist presetId playlistId

    | Telegram.Action.AskForPlaylistSize -> deps.AskForPlaylistSize userId

    |  Telegram.Action.IncludeLikedTracks presetId -> setLikedTracksHandling presetId PresetSettings.LikedTracksHandling.Include
    |  Telegram.Action.ExcludeLikedTracks presetId -> setLikedTracksHandling presetId PresetSettings.LikedTracksHandling.Exclude
    |  Telegram.Action.IgnoreLikedTracks presetId -> setLikedTracksHandling presetId PresetSettings.LikedTracksHandling.Ignore
