namespace Generator.Bot.Services

open Azure.Storage.Queues
open Domain
open Domain.Core
open Domain.Workflows
open Infrastructure
open Resources
open MongoDB.Driver
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
    _bot: ITelegramBotClient,
    _queueClient: QueueClient,
    _connectionMultiplexer: IConnectionMultiplexer,
    loadPreset: Preset.Load,
    updatePreset: Preset.Update,
    loadUser: User.Load,
    _database: IMongoDatabase
  ) =

  member this.ProcessAsync(callbackQuery: CallbackQuery) =
    let userId = callbackQuery.From.Id |> UserId

    let updateUser = User.update _database
    let enableIncludedPlaylist = IncludedPlaylist.enable loadPreset updatePreset
    let disableIncludedPlaylist = IncludedPlaylist.disable loadPreset updatePreset
    let removeTargetedPlaylist = TargetedPlaylist.remove loadPreset updatePreset

    let askForReply = Telegram.askForReply _bot userId callbackQuery.Message.MessageId
    let editMessage = Telegram.editMessage _bot callbackQuery.Message.MessageId userId
    let answerCallbackQuery = Telegram.answerCallbackQuery _bot callbackQuery.Id
    let countPlaylistTracks = Playlist.countTracks _connectionMultiplexer
    let appendToTargetedPlaylist = TargetedPlaylist.appendToTargetedPlaylist loadPreset updatePreset
    let overwriteTargetedPlaylist = TargetedPlaylist.overwriteTargetedPlaylist loadPreset updatePreset
    let setCurrentPreset = Domain.Workflows.User.setCurrentPreset loadUser updateUser
    let setLikedTracksHandling = Preset.setLikedTracksHandling loadPreset updatePreset
    let enableRecommendations = Preset.enableRecommendations loadPreset updatePreset
    let disableRecommendations = Preset.disableRecommendations loadPreset updatePreset

    let removePreset = Preset.remove _database
    let removePreset = Domain.Workflows.Preset.remove loadUser removePreset updateUser

    let showUserPresets = Workflows.sendUserPresets editMessage loadUser
    let removePreset = Workflows.CallbackQuery.removePreset removePreset showUserPresets

    let sendPresetInfo =
      Workflows.sendPresetInfo loadPreset editMessage

    let setCurrentPreset = Telegram.Workflows.setCurrentPreset answerCallbackQuery setCurrentPreset

    let showIncludedPlaylists =
      Workflows.showIncludedPlaylists loadPreset editMessage

    let showExcludedPlaylists =
      Workflows.showExcludedPlaylists loadPreset editMessage

    let showTargetedPlaylists = Workflows.showTargetedPlaylists loadPreset editMessage

    let showIncludedPlaylist = Workflows.showIncludedPlaylist editMessage loadPreset countPlaylistTracks
    let showExcludedPlaylist = Workflows.showExcludedPlaylist editMessage loadPreset countPlaylistTracks
    let showTargetedPlaylist = Workflows.showTargetedPlaylist editMessage loadPreset countPlaylistTracks

    let enableIncludedPlaylist = Workflows.enableIncludedPlaylist enableIncludedPlaylist answerCallbackQuery showIncludedPlaylist
    let disableIncludedPlaylist = Workflows.disableIncludedPlaylist disableIncludedPlaylist answerCallbackQuery showIncludedPlaylist

    let removeIncludedPlaylist = Workflows.removeIncludedPlaylist answerCallbackQuery
    let removeExcludedPlaylist = Workflows.removeExcludedPlaylist answerCallbackQuery

    let removeTargetedPlaylist = Workflows.removeTargetedPlaylist removeTargetedPlaylist answerCallbackQuery showTargetedPlaylists

    let appendToTargetedPlaylist = Workflows.appendToTargetedPlaylist appendToTargetedPlaylist answerCallbackQuery showTargetedPlaylist
    let overwriteTargetedPlaylist = Workflows.overwriteTargetedPlaylist overwriteTargetedPlaylist answerCallbackQuery showTargetedPlaylist

    let setLikedTracksHandling =
      Workflows.setLikedTracksHandling answerCallbackQuery setLikedTracksHandling sendPresetInfo

    let enableRecommendations =
      Workflows.enableRecommendations enableRecommendations answerCallbackQuery sendPresetInfo
    let disableRecommendations =
      Workflows.disableRecommendations disableRecommendations answerCallbackQuery sendPresetInfo

    match callbackQuery.Data |> Workflows.parseAction with
    | Action.ShowPresetInfo presetId -> sendPresetInfo presetId
    | Action.SetCurrentPreset presetId -> setCurrentPreset userId presetId
    | Action.RemovePreset presetId -> removePreset presetId

    | Action.ShowIncludedPlaylists(presetId, page) -> showIncludedPlaylists presetId page
    | Action.ShowIncludedPlaylist(presetId, playlistId) -> showIncludedPlaylist presetId playlistId
    | Action.EnableIncludedPlaylist(presetId, playlistId) -> enableIncludedPlaylist presetId playlistId
    | Action.DisableIncludedPlaylist(presetId, playlistId) -> disableIncludedPlaylist presetId playlistId
    | Action.RemoveIncludedPlaylist(presetId, playlistId) -> removeIncludedPlaylist presetId playlistId

    | Action.ShowExcludedPlaylists(presetId, page) -> showExcludedPlaylists presetId page
    | Action.ShowExcludedPlaylist(presetId, playlistId) -> showExcludedPlaylist presetId playlistId
    | Action.RemoveExcludedPlaylist(presetId, playlistId) -> removeExcludedPlaylist presetId playlistId

    | Action.ShowTargetedPlaylists(presetId, page) -> showTargetedPlaylists presetId page
    | Action.ShowTargetedPlaylist(presetId, playlistId) -> showTargetedPlaylist presetId playlistId
    | Action.AppendToTargetedPlaylist(presetId, playlistId) -> appendToTargetedPlaylist presetId playlistId
    | Action.OverwriteTargetedPlaylist(presetId, playlistId) -> overwriteTargetedPlaylist presetId playlistId
    | Action.RemoveTargetedPlaylist(presetId, playlistId) -> removeTargetedPlaylist presetId playlistId

    | Action.AskForPlaylistSize -> askForReply Messages.SendPlaylistSize

    | Action.IncludeLikedTracks presetId -> setLikedTracksHandling presetId PresetSettings.LikedTracksHandling.Include
    | Action.ExcludeLikedTracks presetId -> setLikedTracksHandling presetId PresetSettings.LikedTracksHandling.Exclude
    | Action.IgnoreLikedTracks presetId -> setLikedTracksHandling presetId PresetSettings.LikedTracksHandling.Ignore

    | Action.EnableRecommendations presetId -> enableRecommendations presetId
    | Action.DisableRecommendations presetId -> disableRecommendations presetId

    | Action.ShowUserPresets -> showUserPresets userId