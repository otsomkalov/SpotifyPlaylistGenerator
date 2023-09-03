namespace Generator.Bot.Services

open System.Threading.Tasks
open Azure.Storage.Queues
open Database
open Domain
open Domain.Core
open Generator.Bot
open Generator.Bot.Constants
open Infrastructure.Workflows
open Telegram.Bot
open Telegram.Bot.Types
open Generator.Bot.Helpers
open Domain.Extensions

[<NoEquality; NoComparison>]
type ProcessCallbackQueryDeps ={
  LoadPreset: Workflows.Preset.Load
  ShowIncludedPlaylists: Telegram.ShowIncludedPlaylists
  ShowExcludedPlaylists: Telegram.ShowExcludedPlaylists
  ShowTargetPlaylists: Telegram.ShowTargetPlaylists
}

type CallbackQueryService
  (
    _setPlaylistSizeCommandHandler: SetPlaylistSizeCommandHandler,
    _context: AppDbContext,
    _bot: ITelegramBotClient,
    sendPresetInfo: Telegram.SendPresetInfo,
    _queueClient: QueueClient,
    setCurrentPreset: Telegram.SetCurrentPreset,
    deps: ProcessCallbackQueryDeps,
    getPresetMessage: Telegram.GetPresetMessage
  ) =

  let appendToTargetPlaylist userId playlistId (callbackQuery: CallbackQuery) =
    task {
      do! TargetPlaylist.appendToTargetPlaylist _context userId playlistId

      let! _ = _bot.AnswerCallbackQueryAsync(callbackQuery.Id, "Target playlist will be appended with generated tracks")

      return ()
    }

  let overwriteTargetPlaylist userId playlistId (callbackQuery: CallbackQuery) =
    task {
      do! TargetPlaylist.overwriteTargetPlaylist _context userId playlistId

      let! _ = _bot.AnswerCallbackQueryAsync(callbackQuery.Id, "Target playlist will be overwritten with generated tracks")

      return ()
    }

  let showIncludedPlaylists userId presetId page (callbackQuery: CallbackQuery) =
    let showIncludedPlaylists' =
      deps.ShowIncludedPlaylists callbackQuery.Message.MessageId page userId >> Async.AwaitTask

    presetId |> (deps.LoadPreset >> Async.bind showIncludedPlaylists' >> Async.StartAsTask)

  let showExcludedPlaylists userId presetId page (callbackQuery: CallbackQuery) =
    let showExcludedPlaylists' =
      deps.ShowExcludedPlaylists callbackQuery.Message.MessageId page userId >> Async.AwaitTask

    presetId |> (deps.LoadPreset >> Async.bind showExcludedPlaylists' >> Async.StartAsTask)

  let showTargetPlaylists userId presetId page (callbackQuery: CallbackQuery) =
    let showTargetPlaylists =
      deps.ShowTargetPlaylists callbackQuery.Message.MessageId page userId >> Async.AwaitTask

    presetId |> (deps.LoadPreset >> Async.bind showTargetPlaylists >> Async.StartAsTask)

  let showIncludedPlaylist (callbackQuery: CallbackQuery) =
    task{
      do! _bot.AnswerCallbackQueryAsync(callbackQuery.Id, "Not implemented yet")

      return ()
    }

  let showExcludedPlaylist (callbackQuery: CallbackQuery) =
    task{
      do! _bot.AnswerCallbackQueryAsync(callbackQuery.Id, "Not implemented yet")

      return ()
    }

  let showTargetPlaylist (callbackQuery: CallbackQuery) =
    task{
      do! _bot.AnswerCallbackQueryAsync(callbackQuery.Id, "Not implemented yet")

      return ()
    }

  let showSelectedPreset userId presetId (callbackQuery: CallbackQuery) =
    sendPresetInfo callbackQuery.Message.MessageId userId presetId

  let setCurrentPreset userId presetId (callbackQuery: CallbackQuery) =
    setCurrentPreset callbackQuery.Id userId presetId

  let setLikedTracksHandling userId presetId handling (callbackQuery: CallbackQuery) =
    let updatePresetSettings = Preset.updateSettings _context presetId
    let setLikedTracksHandling = Preset.setLikedTracksHandling deps.LoadPreset updatePresetSettings
    let sendPresetInfo = Telegram.sendPresetInfo _bot getPresetMessage

    let setLikedTracksHandling = Telegram.setLikedTracksHandling _bot setLikedTracksHandling sendPresetInfo callbackQuery.Id callbackQuery.Message.MessageId userId

    setLikedTracksHandling presetId handling

  member this.ProcessAsync(callbackQuery: CallbackQuery) =
    task {
      let processCallbackQueryDataTask: CallbackQuery -> Task<unit> =
        let userId = callbackQuery.From.Id |> UserId

        match callbackQuery.Data with
        | PresetAction(id, CallbackQueryConstants.includeLikedTracks) ->
          setLikedTracksHandling userId id PresetSettings.LikedTracksHandling.Include
        | PresetAction(id, CallbackQueryConstants.excludeLikedTracks) ->
          setLikedTracksHandling userId id PresetSettings.LikedTracksHandling.Exclude
        | PresetAction(id, CallbackQueryConstants.ignoreLikedTracks) ->
          setLikedTracksHandling userId id PresetSettings.LikedTracksHandling.Ignore
        | CallbackQueryData("tp", id, "a") -> appendToTargetPlaylist userId (id |> PlaylistId |> WritablePlaylistId)
        | CallbackQueryData("tp", id, "o") -> overwriteTargetPlaylist userId (id |> PlaylistId |> WritablePlaylistId)
        | CallbackQueryData("ip", id, "i") -> showIncludedPlaylist
        | CallbackQueryData("ep", id, "i") -> showExcludedPlaylist
        | CallbackQueryData("tp", id, "i") -> showTargetPlaylist
        | CallbackQueryDataWithPage("p", id, "ip", page) -> showIncludedPlaylists userId (id |> int |> PresetId) page
        | CallbackQueryDataWithPage("p", id, "ep", page) -> showExcludedPlaylists userId (id |> int |> PresetId) page
        | CallbackQueryDataWithPage("p", id, "tp", page) -> showTargetPlaylists userId (id |> int |> PresetId) page
        | PresetAction(id, "i") -> showSelectedPreset userId id
        | PresetAction(id, "c") -> setCurrentPreset userId id

      return! processCallbackQueryDataTask callbackQuery
    }
