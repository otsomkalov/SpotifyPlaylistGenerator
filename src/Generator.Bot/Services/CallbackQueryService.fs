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
    let askForReply = Telegram.askForReply _bot userId callbackQuery.Message.MessageId
    let editMessage = Telegram.editMessage _bot callbackQuery.Message.MessageId userId
    let answerCallbackQuery = Telegram.answerCallbackQuery _bot callbackQuery.Id
    let countPlaylistTracks = Playlist.countTracks _connectionMultiplexer

    let removePreset = Preset.remove _database

    let showUserPresets = Workflows.sendUserPresets editMessage loadUser

    let sendPresetInfo = Workflows.sendPresetInfo loadPreset editMessage

    let showIncludedPlaylists = Workflows.showIncludedPlaylists loadPreset editMessage
    let showExcludedPlaylists = Workflows.showExcludedPlaylists loadPreset editMessage
    let showTargetedPlaylists = Workflows.showTargetedPlaylists loadPreset editMessage

    let showIncludedPlaylist = Workflows.showIncludedPlaylist editMessage loadPreset countPlaylistTracks
    let showExcludedPlaylist = Workflows.showExcludedPlaylist editMessage loadPreset countPlaylistTracks
    let showTargetedPlaylist = Workflows.showTargetedPlaylist editMessage loadPreset countPlaylistTracks

    match callbackQuery.Data |> Workflows.parseAction with
    | Action.ShowPresetInfo presetId -> sendPresetInfo presetId
    | Action.SetCurrentPreset presetId ->
      let setCurrentPreset = Domain.Workflows.User.setCurrentPreset loadUser updateUser
      let setCurrentPreset = Workflows.setCurrentPreset answerCallbackQuery setCurrentPreset

      setCurrentPreset userId presetId
    | Action.RemovePreset presetId ->
      let removePreset = Domain.Workflows.Preset.remove loadUser removePreset updateUser
      let removePreset = Workflows.CallbackQuery.removePreset removePreset showUserPresets

      removePreset presetId

    | Action.ShowIncludedPlaylists(presetId, page) -> showIncludedPlaylists presetId page
    | Action.ShowIncludedPlaylist(presetId, playlistId) -> showIncludedPlaylist presetId playlistId
    | Action.EnableIncludedPlaylist(presetId, playlistId) ->
      let enableIncludedPlaylist = IncludedPlaylist.enable loadPreset updatePreset
      let enableIncludedPlaylist = Workflows.IncludedPlaylist.enable enableIncludedPlaylist answerCallbackQuery showIncludedPlaylist

      enableIncludedPlaylist presetId playlistId
    | Action.DisableIncludedPlaylist(presetId, playlistId) ->
      let disableIncludedPlaylist = IncludedPlaylist.disable loadPreset updatePreset
      let disableIncludedPlaylist = Workflows.IncludedPlaylist.enable disableIncludedPlaylist answerCallbackQuery showIncludedPlaylist

      disableIncludedPlaylist presetId playlistId
    | Action.RemoveIncludedPlaylist(presetId, playlistId) ->
      let removeIncludedPlaylist = Workflows.removeIncludedPlaylist answerCallbackQuery

      removeIncludedPlaylist presetId playlistId
    | Action.ShowExcludedPlaylists(presetId, page) -> showExcludedPlaylists presetId page
    | Action.ShowExcludedPlaylist(presetId, playlistId) -> showExcludedPlaylist presetId playlistId
    | Action.EnableExcludedPlaylist(presetId, playlistId) ->
      let enableExcludedPlaylist = ExcludedPlaylist.enable loadPreset updatePreset
      let enableExcludedPlaylist = Workflows.ExcludedPlaylist.enable enableExcludedPlaylist answerCallbackQuery showExcludedPlaylist

      enableExcludedPlaylist presetId playlistId
    | Action.DisableExcludedPlaylist(presetId, playlistId) ->
      let disableExcludedPlaylist = ExcludedPlaylist.disable loadPreset updatePreset
      let disableExcludedPlaylist = Workflows.ExcludedPlaylist.disable disableExcludedPlaylist answerCallbackQuery showExcludedPlaylist

      disableExcludedPlaylist presetId playlistId
    | Action.RemoveExcludedPlaylist(presetId, playlistId) ->
      let removeExcludedPlaylist = Workflows.removeExcludedPlaylist answerCallbackQuery

      removeExcludedPlaylist presetId playlistId
    | Action.ShowTargetedPlaylists(presetId, page) -> showTargetedPlaylists presetId page
    | Action.ShowTargetedPlaylist(presetId, playlistId) -> showTargetedPlaylist presetId playlistId
    | Action.AppendToTargetedPlaylist(presetId, playlistId) ->
      let appendToTargetedPlaylist = TargetedPlaylist.appendToTargetedPlaylist loadPreset updatePreset
      let appendToTargetedPlaylist = Workflows.appendToTargetedPlaylist appendToTargetedPlaylist answerCallbackQuery showTargetedPlaylist

      appendToTargetedPlaylist presetId playlistId
    | Action.OverwriteTargetedPlaylist(presetId, playlistId) ->
      let overwriteTargetedPlaylist = TargetedPlaylist.overwriteTargetedPlaylist loadPreset updatePreset
      let overwriteTargetedPlaylist = Workflows.overwriteTargetedPlaylist overwriteTargetedPlaylist answerCallbackQuery showTargetedPlaylist

      overwriteTargetedPlaylist presetId playlistId
    | Action.RemoveTargetedPlaylist(presetId, playlistId) ->
      let removeTargetedPlaylist = TargetedPlaylist.remove loadPreset updatePreset
      let removeTargetedPlaylist = Workflows.removeTargetedPlaylist removeTargetedPlaylist answerCallbackQuery showTargetedPlaylists

      removeTargetedPlaylist presetId playlistId
    | Action.AskForPlaylistSize -> askForReply Messages.SendPlaylistSize
    | Action.IncludeLikedTracks presetId ->
      let includeLikedTracks = Preset.includeLikedTracks loadPreset updatePreset
      let includeLikedTracks = Workflows.includeLikedTracks answerCallbackQuery sendPresetInfo includeLikedTracks

      includeLikedTracks presetId
    | Action.ExcludeLikedTracks presetId ->
      let excludeLikedTracks = Preset.excludeLikedTracks loadPreset updatePreset
      let excludeLikedTracks = Workflows.excludeLikedTracks answerCallbackQuery sendPresetInfo excludeLikedTracks

      excludeLikedTracks presetId
    | Action.IgnoreLikedTracks presetId ->
      let ignoreLikedTracks = Preset.ignoreLikedTracks loadPreset updatePreset
      let ignoreLikedTracks = Workflows.ignoreLikedTracks answerCallbackQuery sendPresetInfo ignoreLikedTracks

      ignoreLikedTracks presetId
    | Action.EnableRecommendations presetId ->
      let enableRecommendations = Preset.enableRecommendations loadPreset updatePreset
      let enableRecommendations = Workflows.enableRecommendations enableRecommendations answerCallbackQuery sendPresetInfo

      enableRecommendations presetId
    | Action.DisableRecommendations presetId ->
      let disableRecommendations = Preset.disableRecommendations loadPreset updatePreset
      let disableRecommendations =
        Workflows.disableRecommendations disableRecommendations answerCallbackQuery sendPresetInfo

      disableRecommendations presetId

    | Action.ShowUserPresets -> showUserPresets userId