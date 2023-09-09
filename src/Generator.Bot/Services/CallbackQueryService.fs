namespace Generator.Bot.Services

open Azure.Storage.Queues
open Database
open Domain
open Domain.Core
open Generator.Bot
open Infrastructure.Workflows
open StackExchange.Redis
open Telegram.Bot
open Telegram.Bot.Types

[<NoEquality; NoComparison>]
type ProcessCallbackQueryDeps =
  { LoadPreset: Workflows.Preset.Load
    AskForPlaylistSize: Telegram.AskForPlaylistSize }

type CallbackQueryService
  (
    _setPlaylistSizeCommandHandler: SetPlaylistSizeCommandHandler,
    _context: AppDbContext,
    _bot: ITelegramBotClient,
    _queueClient: QueueClient,
    deps: ProcessCallbackQueryDeps,
    getPresetMessage: Telegram.GetPresetMessage,
    _connectionMultiplexer: IConnectionMultiplexer
  ) =

  let appendToTargetPlaylist callbackQueryId presetId playlistId =
    task {
      do! TargetPlaylist.appendToTargetPlaylist _context presetId playlistId

      let! _ = _bot.AnswerCallbackQueryAsync(callbackQueryId, "Target playlist will be appended with generated tracks")

      return ()
    }

  let overwriteTargetPlaylist callbackQueryId presetId playlistId =
    task {
      do! TargetPlaylist.overwriteTargetPlaylist _context presetId playlistId

      let! _ = _bot.AnswerCallbackQueryAsync(callbackQueryId, "Target playlist will be overwritten with generated tracks")

      return ()
    }


  let showExcludedPlaylist callbackQueryId presetId playlistId =
    task {
      do! _bot.AnswerCallbackQueryAsync(callbackQueryId, "Not implemented yet")

      return ()
    }

  let showTargetPlaylist callbackQueryId presetId playlistId =
    task {
      do! _bot.AnswerCallbackQueryAsync(callbackQueryId, "Not implemented yet")

      return ()
    }

  let setLikedTracksHandling callbackQueryId messageId userId presetId handling =
    let updatePresetSettings = Preset.updateSettings _context presetId

    let setLikedTracksHandling =
      Preset.setLikedTracksHandling deps.LoadPreset updatePresetSettings

    let sendPresetInfo = Telegram.sendPresetInfo _bot getPresetMessage messageId userId

    let setLikedTracksHandling =
      Telegram.setLikedTracksHandling _bot setLikedTracksHandling sendPresetInfo callbackQueryId

    setLikedTracksHandling presetId handling

  member this.ProcessAsync(callbackQuery: CallbackQuery) =
    let userId = callbackQuery.From.Id |> UserId

    let editMessage = Telegram.editMessage _bot callbackQuery.Message.MessageId userId
    let countPlaylistTracks = Playlist.countTracks _connectionMultiplexer

    let sendPresetInfo =
      Telegram.sendPresetInfo _bot getPresetMessage callbackQuery.Message.MessageId userId

    let setCurrentPreset = Telegram.setCurrentPreset _bot _context callbackQuery.Id

    let showIncludedPlaylists =
      Telegram.showIncludedPlaylists deps.LoadPreset editMessage

    let showExcludedPlaylists =
      Telegram.showExcludedPlaylists deps.LoadPreset editMessage

    let showTargetPlaylists = Telegram.showTargetPlaylists deps.LoadPreset editMessage

    let showIncludedPlaylist = Telegram.showIncludedPlaylist editMessage deps.LoadPreset countPlaylistTracks
    let showExcludedPlaylist = showExcludedPlaylist callbackQuery.Id
    let showTargetPlaylist = showTargetPlaylist callbackQuery.Id

    let removeIncludedPlaylist = Telegram.removeIncludedPlaylistBuilder _bot callbackQuery.Id

    let appendToTargetPlaylist = appendToTargetPlaylist callbackQuery.Id
    let overwriteTargetPlaylist = overwriteTargetPlaylist callbackQuery.Id

    let setLikedTracksHandling =
      setLikedTracksHandling callbackQuery.Id callbackQuery.Message.MessageId userId

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

    | Telegram.Action.AppendToTargetPlaylist(presetId, playlistId) -> appendToTargetPlaylist presetId playlistId
    | Telegram.Action.OverwriteTargetPlaylist(presetId, playlistId) -> overwriteTargetPlaylist presetId playlistId

    | Telegram.Action.AskForPlaylistSize -> deps.AskForPlaylistSize userId

    | Telegram.Action.IncludeLikedTracks presetId -> setLikedTracksHandling presetId PresetSettings.LikedTracksHandling.Include
    | Telegram.Action.ExcludeLikedTracks presetId -> setLikedTracksHandling presetId PresetSettings.LikedTracksHandling.Exclude
    | Telegram.Action.IgnoreLikedTracks presetId -> setLikedTracksHandling presetId PresetSettings.LikedTracksHandling.Ignore
