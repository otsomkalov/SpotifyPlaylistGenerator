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

    let enableIncludedPlaylist = IncludedPlaylist.enable _context
    let disableIncludedPlaylist = IncludedPlaylist.disable _context
    let removeTargetPlaylist = TargetPlaylist.remove _context

    let editMessage = Telegram.editMessage _bot callbackQuery.Message.MessageId userId
    let answerCallbackQuery = Telegram.answerCallbackQuery _bot callbackQuery.Id
    let countPlaylistTracks = Playlist.countTracks _connectionMultiplexer
    let updateTargetPlaylist = TargetPlaylist.update _context
    let appendToTargetPlaylist = Workflows.TargetPlaylist.appendToTargetPlaylist deps.LoadPreset updateTargetPlaylist
    let overwriteTargetPlaylist = Workflows.TargetPlaylist.overwriteTargetPlaylist deps.LoadPreset updateTargetPlaylist

    let sendPresetInfo =
      Telegram.sendPresetInfo _bot getPresetMessage callbackQuery.Message.MessageId userId

    let setCurrentPreset = Telegram.setCurrentPreset _bot _context callbackQuery.Id

    let showIncludedPlaylists =
      Telegram.showIncludedPlaylists deps.LoadPreset editMessage

    let showExcludedPlaylists =
      Telegram.showExcludedPlaylists deps.LoadPreset editMessage

    let showTargetPlaylists = Telegram.showTargetPlaylists deps.LoadPreset editMessage

    let showIncludedPlaylist = Telegram.showIncludedPlaylist editMessage deps.LoadPreset countPlaylistTracks
    let showExcludedPlaylist = Telegram.showExcludedPlaylist editMessage deps.LoadPreset countPlaylistTracks
    let showTargetPlaylist = Telegram.showTargetPlaylist editMessage deps.LoadPreset countPlaylistTracks

    let enableIncludedPlaylist = Telegram.enableIncludedPlaylist enableIncludedPlaylist answerCallbackQuery showIncludedPlaylist
    let disableIncludedPlaylist = Telegram.disableIncludedPlaylist disableIncludedPlaylist answerCallbackQuery showIncludedPlaylist

    let removeIncludedPlaylist = Telegram.removeIncludedPlaylist _bot callbackQuery.Id
    let removeExcludedPlaylist = Telegram.removeExcludedPlaylist _bot callbackQuery.Id

    let removeTargetPlaylist = Telegram.removeTargetPlaylist removeTargetPlaylist answerCallbackQuery showTargetPlaylists

    let appendToTargetPlaylist = Telegram.appendToTargetPlaylist appendToTargetPlaylist answerCallbackQuery showTargetPlaylist
    let overwriteTargetPlaylist = Telegram.overwriteTargetPlaylist overwriteTargetPlaylist answerCallbackQuery showTargetPlaylist

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

    | Telegram.Action.EnableIncludedPlaylist(presetId, playlistId) -> enableIncludedPlaylist presetId playlistId
    | Telegram.Action.DisableIncludedPlaylist(presetId, playlistId) -> disableIncludedPlaylist presetId playlistId

    | Telegram.Action.RemoveIncludedPlaylist(presetId, playlistId) -> removeIncludedPlaylist presetId playlistId
    | Telegram.Action.RemoveExcludedPlaylist(presetId, playlistId) -> removeExcludedPlaylist presetId playlistId
    | Telegram.Action.RemoveTargetPlaylist(presetId, playlistId) -> removeTargetPlaylist presetId playlistId

    | Telegram.Action.AppendToTargetPlaylist(presetId, playlistId) -> appendToTargetPlaylist presetId playlistId
    | Telegram.Action.OverwriteTargetPlaylist(presetId, playlistId) -> overwriteTargetPlaylist presetId playlistId

    | Telegram.Action.AskForPlaylistSize -> deps.AskForPlaylistSize userId

    | Telegram.Action.IncludeLikedTracks presetId -> setLikedTracksHandling presetId PresetSettings.LikedTracksHandling.Include
    | Telegram.Action.ExcludeLikedTracks presetId -> setLikedTracksHandling presetId PresetSettings.LikedTracksHandling.Exclude
    | Telegram.Action.IgnoreLikedTracks presetId -> setLikedTracksHandling presetId PresetSettings.LikedTracksHandling.Ignore
