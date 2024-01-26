module Infrastructure.Telegram.Services

open System.Reflection
open Resources
open Telegram
open Infrastructure
open Infrastructure.Telegram.Helpers
open Infrastructure.Workflows
open System.Collections.Generic
open System.Threading.Tasks
open Azure.Storage.Queues
open Domain.Core
open Domain.Workflows
open Infrastructure.Settings
open Infrastructure.Spotify
open Microsoft.Extensions.Options
open MongoDB.Driver
open SpotifyAPI.Web
open StackExchange.Redis
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums
open Telegram.Core
open System
open otsom.FSharp.Extensions
open otsom.FSharp.Extensions.String

type SpotifyClientProvider(connectionMultiplexer: IConnectionMultiplexer, createClientFromTokenResponse: CreateClientFromTokenResponse) =
  let _clientsByTelegramId =
    Dictionary<int64, ISpotifyClient>()

  member this.GetAsync userId : Task<ISpotifyClient> =
    let userId = userId |> UserId.value

    if _clientsByTelegramId.ContainsKey(userId) then
      _clientsByTelegramId[userId] |> Task.FromResult
    else
      let cache = connectionMultiplexer.GetDatabase Cache.tokensDatabase

      task {
        let! tokenValue = userId |> string |> cache.StringGetAsync

        return!
          match tokenValue.IsNullOrEmpty with
          | true -> Task.FromResult null
          | false ->
            let client =
              AuthorizationCodeTokenResponse(RefreshToken = tokenValue)
              |> createClientFromTokenResponse

            this.SetClient(userId, client)

            client |> Task.FromResult
      }

  member this.SetClient(telegramId: int64, client: ISpotifyClient) =
    if _clientsByTelegramId.ContainsKey(telegramId) then
      ()
    else
      (telegramId, client) |> _clientsByTelegramId.Add

type AuthState =
  | Authorized
  | Unauthorized

type MessageService
  (
    _spotifyClientProvider: SpotifyClientProvider,
    _bot: ITelegramBotClient,
    loadUser: User.Load,
    loadPreset: Preset.Load,
    updatePreset: Preset.Update,
    _database: IMongoDatabase,
    _connectionMultiplexer: IConnectionMultiplexer,
    _spotifyOptions: IOptions<SpotifySettings>,
    _queueClient: QueueClient
  ) =

  let sendUserPresets sendMessage (message: Message) =
    let sendUserPresets = Telegram.Workflows.sendUserPresets sendMessage loadUser
    sendUserPresets (message.From.Id |> UserId)

  member this.ProcessAsync(message: Message) =
    let userId = message.From.Id |> UserId

    let sendMessage = Workflows.sendMessage _bot userId
    let sendLink = Workflows.sendLink _bot userId
    let sendKeyboard = Workflows.sendKeyboard _bot userId
    let replyToMessage = Workflows.replyToMessage _bot userId message.MessageId
    let sendButtons = Workflows.sendButtons _bot userId
    let askForReply = Workflows.askForReply _bot userId message.MessageId
    let savePreset = Preset.save _database
    let updateUser = User.update _database

    let sendCurrentPresetInfo = Telegram.Workflows.sendCurrentPresetInfo loadUser loadPreset sendKeyboard
    let sendSettingsMessage = Telegram.Workflows.sendSettingsMessage loadUser loadPreset sendKeyboard

    let sendLoginMessage () =
      let initState = Auth.initState _connectionMultiplexer
      let getLoginLink = Workflows.Auth.getLoginLink _spotifyOptions

      let getLoginLink = Auth.getLoginLink initState getLoginLink

      getLoginLink userId
      |> Task.bind (sendLink Messages.LoginToSpotify Buttons.Login)

    task {
      let! spotifyClient = _spotifyClientProvider.GetAsync userId

      let authState =
        if spotifyClient = null then
          AuthState.Unauthorized
        else
          AuthState.Authorized

      let parsePlaylistId = Playlist.parseId
      let loadFromSpotify = Playlist.loadFromSpotify spotifyClient

      return!
        match message.Type with
        | MessageType.Text ->
          let includePlaylist = Playlist.includePlaylist parsePlaylistId loadFromSpotify loadPreset updatePreset
          let includePlaylist = Workflows.Playlist.includePlaylist replyToMessage loadUser includePlaylist

          let excludePlaylist = Playlist.excludePlaylist parsePlaylistId loadFromSpotify loadPreset updatePreset
          let excludePlaylist = Workflows.Playlist.excludePlaylist replyToMessage loadUser excludePlaylist

          let targetPlaylist = Playlist.targetPlaylist parsePlaylistId loadFromSpotify loadPreset updatePreset
          let targetPlaylist = Workflows.Playlist.targetPlaylist replyToMessage loadUser targetPlaylist

          let queueGeneration = Workflows.Playlist.queueGeneration _queueClient replyToMessage loadUser loadPreset Preset.validate

          match isNull message.ReplyToMessage with
          | false ->
            match (message.ReplyToMessage.Text, authState) with
            | Equals Messages.SendIncludedPlaylist, Unauthorized
            | Equals Messages.SendExcludedPlaylist, Unauthorized
            | Equals Messages.SendTargetedPlaylist, Unauthorized -> sendLoginMessage()

            | Equals Messages.SendPlaylistSize, _ ->
              match message.Text with
              | Int size ->
                let setPlaylistSize = Preset.setPlaylistSize loadPreset updatePreset
                let setPlaylistSize = Workflows.setPlaylistSize sendMessage sendSettingsMessage loadUser setPlaylistSize

                setPlaylistSize userId size
              | _ ->
                replyToMessage Messages.WrongPlaylistSize
            | Equals Messages.SendIncludedPlaylist, Authorized ->
              includePlaylist userId (Playlist.RawPlaylistId message.Text)
            | Equals Messages.SendExcludedPlaylist, Authorized ->
              excludePlaylist userId (Playlist.RawPlaylistId message.Text)
            | Equals Messages.SendTargetedPlaylist, Authorized ->
              targetPlaylist userId (Playlist.RawPlaylistId message.Text)
            | Equals Messages.SendPresetName, _ ->
              let createPreset = Preset.create savePreset loadUser updateUser userId
              let sendPresetInfo = Telegram.Workflows.sendPresetInfo loadPreset sendButtons
              let createPreset = Telegram.Workflows.Message.createPreset createPreset sendPresetInfo

              createPreset message.Text

            | _ -> replyToMessage "Unknown command"
          | _ ->
            match (message.Text, authState) with
            | StartsWith "/include", Unauthorized | StartsWith "/exclude", Unauthorized | StartsWith "/target", Unauthorized
            | Equals Buttons.IncludePlaylist, Unauthorized | Equals Buttons.ExcludePlaylist, Unauthorized | Equals Buttons.TargetPlaylist, Unauthorized
            | Equals Buttons.GeneratePlaylist, Unauthorized | StartsWith "/generate", Unauthorized | Equals "/start", Unauthorized
             -> sendLoginMessage()

            | Equals "/start", Authorized ->
              sendCurrentPresetInfo userId
            | CommandWithData "/start" state, _ ->
              let tryGetAuth = Auth.tryGetCompletedAuth _connectionMultiplexer
              let getToken = Auth.getToken _spotifyOptions
              let saveCompletedAuth = Auth.saveCompletedAuth _connectionMultiplexer
              let createUserIfNotExists = User.createIfNotExists _database
              let sendErrorMessage =
                function
                | Auth.CompleteError.StateNotFound ->
                  replyToMessage "State not found. Try to login via fresh link."
                | Auth.CompleteError.StateDoesntBelongToUser ->
                  replyToMessage "State provided does not belong to your login request. Try to login via fresh link."

              let completeAuth = Auth.complete tryGetAuth getToken saveCompletedAuth createUserIfNotExists

              completeAuth userId (state |> Auth.State.parse)
              |> TaskResult.taskEither (fun () -> sendCurrentPresetInfo userId) sendErrorMessage
            | Equals "/help", _ ->
              sendMessage Messages.Help
            | Equals "/guide", _ -> sendMessage Messages.Guide
            | Equals "/privacy", _ -> sendMessage Messages.Privacy
            | Equals "/faq", _ -> sendMessage Messages.FAQ
            | Equals "/generate", Authorized -> queueGeneration userId
            | Equals "/version", Authorized ->
              sendMessage (Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion)
            | CommandWithData "/include" rawPlaylistId, Authorized ->
              if String.IsNullOrEmpty rawPlaylistId then
                replyToMessage "You have entered empty playlist url"
              else
                includePlaylist userId (rawPlaylistId |> Playlist.RawPlaylistId)
            | CommandWithData "/exclude" rawPlaylistId, Authorized ->
              if String.IsNullOrEmpty rawPlaylistId then
                replyToMessage "You have entered empty playlist url"
              else
                excludePlaylist userId (rawPlaylistId |> Playlist.RawPlaylistId)
            | CommandWithData "/target" rawPlaylistId, Authorized ->
              if String.IsNullOrEmpty rawPlaylistId then
                replyToMessage "You have entered empty playlist url"
              else
                targetPlaylist userId (rawPlaylistId |> Playlist.RawPlaylistId)
            | Equals Buttons.SetPlaylistSize, _ -> askForReply Messages.SendPlaylistSize
            | Equals Buttons.CreatePreset, _ -> askForReply Messages.SendPresetName
            | Equals Buttons.GeneratePlaylist, Authorized -> queueGeneration userId
            | Equals Buttons.MyPresets, _ -> sendUserPresets sendButtons message
            | Equals Buttons.Settings, _ -> sendSettingsMessage userId
            | Equals Buttons.IncludePlaylist, Authorized -> askForReply Messages.SendIncludedPlaylist
            | Equals Buttons.ExcludePlaylist, Authorized -> askForReply Messages.SendExcludedPlaylist
            | Equals Buttons.TargetPlaylist, Authorized -> askForReply Messages.SendTargetedPlaylist
            | Equals "Back", _ -> sendCurrentPresetInfo userId

            | _ -> replyToMessage "Unknown command"
        | _ -> Task.FromResult()
    }

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
    let askForReply = Workflows.askForReply _bot userId callbackQuery.Message.MessageId
    let editMessage = Workflows.editMessage _bot callbackQuery.Message.MessageId userId
    let answerCallbackQuery = Workflows.answerCallbackQuery _bot callbackQuery.Id
    let countPlaylistTracks = Playlist.countTracks _connectionMultiplexer


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
      let removePreset = Workflows.Preset.remove _database
      let removePreset = Preset.remove loadUser removePreset updateUser
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
      let removeIncludedPlaylist = IncludedPlaylist.remove loadPreset updatePreset
      let removeIncludedPlaylist = Workflows.removeIncludedPlaylist removeIncludedPlaylist answerCallbackQuery showIncludedPlaylists

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
      let removeExcludedPlaylist = ExcludedPlaylist.remove loadPreset updatePreset
      let removeExcludedPlaylist = Workflows.removeExcludedPlaylist removeExcludedPlaylist answerCallbackQuery showExcludedPlaylists

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