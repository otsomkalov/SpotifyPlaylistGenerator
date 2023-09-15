namespace Generator.Bot.Services

open Azure.Storage.Queues
open Database
open Domain
open Domain.Core
open Infrastructure
open Telegram
open Telegram.Core
open Generator.Bot
open Infrastructure.Workflows
open StackExchange.Redis
open Telegram.Bot
open Telegram.Bot.Types

type CallbackQueryService
  (
    _setPlaylistSizeCommandHandler: SetPlaylistSizeCommandHandler,
    _context: AppDbContext,
    _bot: ITelegramBotClient,
    _queueClient: QueueClient,
    getPresetMessage: GetPresetMessage,
    _connectionMultiplexer: IConnectionMultiplexer
  ) =

  member this.ProcessAsync(callbackQuery: CallbackQuery) =
    let userId = callbackQuery.From.Id |> UserId

    let enableIncludedPlaylist = IncludedPlaylist.enable _context
    let disableIncludedPlaylist = IncludedPlaylist.disable _context
    let removeTargetPlaylist = TargetPlaylist.remove _context
    let listPresets = User.listPresets _context

    let sendMessage = Telegram.sendMessage _bot userId
    let editMessage = Telegram.editMessage _bot callbackQuery.Message.MessageId userId
    let answerCallbackQuery = Telegram.answerCallbackQuery _bot callbackQuery.Id
    let countPlaylistTracks = Playlist.countTracks _connectionMultiplexer
    let appendToTargetPlaylist = TargetPlaylist.appendToTargetPlaylist _context
    let overwriteTargetPlaylist = TargetPlaylist.overwriteTargetPlaylist _context
    let updateSettings = Preset.updateSettings _context
    let loadPreset = Preset.load _context

    let setLikedTracksHandling = Preset.setLikedTracksHandling loadPreset updateSettings
    let askForPlaylistSize = Workflows.askForPlaylistSize sendMessage

    let sendPresetInfo =
      Workflows.sendPresetInfo editMessage getPresetMessage

    let setCurrentPreset = Infrastructure.Workflows.User.setCurrentPreset _context
    let setCurrentPreset = Workflows.setCurrentPreset answerCallbackQuery setCurrentPreset
    let showUserPresets = Workflows.sendUserPresets editMessage listPresets

    let showIncludedPlaylists =
      Workflows.showIncludedPlaylists loadPreset editMessage

    let showExcludedPlaylists =
      Workflows.showExcludedPlaylists loadPreset editMessage

    let showTargetPlaylists = Workflows.showTargetPlaylists loadPreset editMessage

    let showIncludedPlaylist = Workflows.showIncludedPlaylist editMessage loadPreset countPlaylistTracks
    let showExcludedPlaylist = Workflows.showExcludedPlaylist editMessage loadPreset countPlaylistTracks
    let showTargetPlaylist = Workflows.showTargetPlaylist editMessage loadPreset countPlaylistTracks

    let enableIncludedPlaylist = Workflows.enableIncludedPlaylist enableIncludedPlaylist answerCallbackQuery showIncludedPlaylist
    let disableIncludedPlaylist = Workflows.disableIncludedPlaylist disableIncludedPlaylist answerCallbackQuery showIncludedPlaylist

    let removeIncludedPlaylist = Workflows.removeIncludedPlaylist answerCallbackQuery
    let removeExcludedPlaylist = Workflows.removeExcludedPlaylist answerCallbackQuery

    let removeTargetPlaylist = Workflows.removeTargetPlaylist removeTargetPlaylist answerCallbackQuery showTargetPlaylists

    let appendToTargetPlaylist = Workflows.appendToTargetPlaylist appendToTargetPlaylist answerCallbackQuery showTargetPlaylist
    let overwriteTargetPlaylist = Workflows.overwriteTargetPlaylist overwriteTargetPlaylist answerCallbackQuery showTargetPlaylist

    let setLikedTracksHandling =
      Workflows.setLikedTracksHandling answerCallbackQuery setLikedTracksHandling sendPresetInfo

    match callbackQuery.Data |> Workflows.parseAction with
    | Action.ShowPresetInfo presetId -> sendPresetInfo presetId
    | Action.SetCurrentPreset presetId -> setCurrentPreset userId presetId

    | Action.ShowIncludedPlaylists(presetId, page) -> showIncludedPlaylists presetId page
    | Action.ShowIncludedPlaylist(presetId, playlistId) -> showIncludedPlaylist presetId playlistId
    | Action.EnableIncludedPlaylist(presetId, playlistId) -> enableIncludedPlaylist presetId playlistId
    | Action.DisableIncludedPlaylist(presetId, playlistId) -> disableIncludedPlaylist presetId playlistId
    | Action.RemoveIncludedPlaylist(presetId, playlistId) -> removeIncludedPlaylist presetId playlistId

    | Action.ShowExcludedPlaylists(presetId, page) -> showExcludedPlaylists presetId page
    | Action.ShowExcludedPlaylist(presetId, playlistId) -> showExcludedPlaylist presetId playlistId
    | Action.RemoveExcludedPlaylist(presetId, playlistId) -> removeExcludedPlaylist presetId playlistId

    | Action.ShowTargetPlaylists(presetId, page) -> showTargetPlaylists presetId page
    | Action.ShowTargetPlaylist(presetId, playlistId) -> showTargetPlaylist presetId playlistId
    | Action.AppendToTargetPlaylist(presetId, playlistId) -> appendToTargetPlaylist presetId playlistId
    | Action.OverwriteTargetPlaylist(presetId, playlistId) -> overwriteTargetPlaylist presetId playlistId
    | Action.RemoveTargetPlaylist(presetId, playlistId) -> removeTargetPlaylist presetId playlistId

    | Action.AskForPlaylistSize -> askForPlaylistSize userId

    | Action.IncludeLikedTracks presetId -> setLikedTracksHandling presetId PresetSettings.LikedTracksHandling.Include
    | Action.ExcludeLikedTracks presetId -> setLikedTracksHandling presetId PresetSettings.LikedTracksHandling.Exclude
    | Action.IgnoreLikedTracks presetId -> setLikedTracksHandling presetId PresetSettings.LikedTracksHandling.Ignore

    | Action.ShowUserPresets -> showUserPresets userId