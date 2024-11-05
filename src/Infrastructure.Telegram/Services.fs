module Infrastructure.Telegram.Services

open System.Reflection
open Microsoft.ApplicationInsights
open Resources
open Telegram
open Infrastructure
open Infrastructure.Telegram.Helpers
open Infrastructure.Workflows
open System.Threading.Tasks
open Azure.Storage.Queues
open Domain.Core
open Domain.Workflows
open Microsoft.Extensions.Options
open MongoDB.Driver
open StackExchange.Redis
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Core
open System
open otsom.fs.Extensions
open otsom.fs.Extensions.String
open otsom.fs.Telegram.Bot.Auth.Spotify
open otsom.fs.Telegram.Bot.Auth.Spotify.Settings
open otsom.fs.Telegram.Bot.Core
open otsom.fs.Core
open Infrastructure.Repos

type AuthState =
  | Authorized
  | Unauthorized

type MessageService
  (
    _bot: ITelegramBotClient,
    _database: IMongoDatabase,
    _connectionMultiplexer: IConnectionMultiplexer,
    _spotifyOptions: IOptions<SpotifySettings>,
    _queueClient: QueueClient,
    initAuth: Auth.Init,
    completeAuth: Auth.Complete,
    sendUserMessage: SendUserMessage,
    replyToUserMessage: ReplyToUserMessage,
    sendUserKeyboard: SendUserKeyboard,
    sendUserMessageButtons: SendUserMessageButtons,
    askUserForReply: AskUserForReply,
    getSpotifyClient: Spotify.GetClient,
    getPreset: Preset.Get,
    validatePreset: Preset.Validate
  ) =

  member this.ProcessAsync(message: Message) =
    let userId = message.From.Id |> UserId

    let savePreset = PresetRepo.save _database

    let sendMessage = sendUserMessage userId
    let sendKeyboard = sendUserKeyboard userId
    let replyToMessage = replyToUserMessage userId message.MessageId
    let sendButtons = sendUserMessageButtons userId
    let askForReply = askUserForReply userId message.MessageId
    let updateUser = UserRepo.update _database
    let loadUser = UserRepo.load _database
    let getUser = User.get loadUser
    let sendLink = Repos.sendLink _bot userId
    let sendLoginMessage = Telegram.Workflows.sendLoginMessage initAuth sendLink

    let sendCurrentPresetInfo =
      Telegram.Workflows.User.sendCurrentPreset getUser getPreset sendKeyboard

    let sendSettingsMessage =
      Telegram.Workflows.User.sendCurrentPresetSettings getUser getPreset sendKeyboard

    getSpotifyClient userId
    |> Task.bind (function
      | Some client ->
        let parsePlaylistId = Playlist.parseId

        let loadFromSpotify = Playlist.loadFromSpotify client

        let includePlaylist =
          Playlist.includePlaylist parsePlaylistId loadFromSpotify getPreset savePreset

        let includePlaylist =
          Workflows.CurrentPreset.includePlaylist replyToMessage getUser includePlaylist

        let excludePlaylist =
          Playlist.excludePlaylist parsePlaylistId loadFromSpotify getPreset savePreset

        let excludePlaylist =
          Workflows.Playlist.excludePlaylist replyToMessage getUser excludePlaylist

        let targetPlaylist =
          Playlist.targetPlaylist parsePlaylistId loadFromSpotify getPreset savePreset

        let targetPlaylist =
          Workflows.Playlist.targetPlaylist replyToMessage getUser targetPlaylist

        let queuePresetRun = PresetRepo.queueRun _queueClient userId
        let queuePresetRun = Domain.Workflows.Preset.queueRun getPreset validatePreset queuePresetRun
        let queueCurrentPresetRun =
          Workflows.User.queueCurrentPresetRun queuePresetRun sendMessage loadUser (fun _ -> Task.FromResult())

        match isNull message.ReplyToMessage with
        | false ->
          match message.ReplyToMessage.Text with
          | Equals Messages.SendPresetSize ->
            let setTargetPresetSize =
              PresetSettings.setPresetSize getPreset savePreset

            let setCurrentPresetSize = User.setCurrentPresetSize getUser setTargetPresetSize

            let setTargetPresetSize =
              Workflows.User.setCurrentPresetSize sendMessage sendSettingsMessage setCurrentPresetSize

            setTargetPresetSize userId (PresetSettings.RawPresetSize message.Text)
          | Equals Messages.SendIncludedPlaylist -> includePlaylist userId (Playlist.RawPlaylistId message.Text)
          | Equals Messages.SendExcludedPlaylist -> excludePlaylist userId (Playlist.RawPlaylistId message.Text)
          | Equals Messages.SendTargetedPlaylist -> targetPlaylist userId (Playlist.RawPlaylistId message.Text)
          | Equals Messages.SendPresetName ->
            let createPreset = ((User.createPreset savePreset loadUser updateUser) |> Telegram.Workflows.User.createPreset sendButtons)

            createPreset userId message.Text
        | _ ->
          match message.Text with
          | Equals "/start" -> sendCurrentPresetInfo userId
          | CommandWithData "/start" state ->
            let processSuccessfulLogin =
              let create = UserRepo.create _database
              let exists = UserRepo.exists _database
              let createUserIfNotExists = User.createIfNotExists exists create

              fun () ->
                task {
                  do! createUserIfNotExists userId
                  do! sendCurrentPresetInfo userId
                }

            let sendErrorMessage =
              function
              | Auth.CompleteError.StateNotFound -> replyToMessage "State not found. Try to login via fresh link."
              | Auth.CompleteError.StateDoesntBelongToUser ->
                replyToMessage "State provided does not belong to your login request. Try to login via fresh link."

            completeAuth userId state
            |> TaskResult.taskEither processSuccessfulLogin (sendErrorMessage >> Task.ignore)
          | Equals "/help" -> sendMessage Messages.Help |> Task.ignore
          | Equals "/guide" -> sendMessage Messages.Guide  |> Task.ignore
          | Equals "/privacy" -> sendMessage Messages.Privacy |> Task.ignore
          | Equals "/faq" -> sendMessage Messages.FAQ |> Task.ignore
          | Equals "/generate" -> queueCurrentPresetRun userId
          | Equals "/version" ->
            sendMessage (
              Assembly
                .GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                .InformationalVersion
            ) |> Task.ignore
          | CommandWithData "/include" rawPlaylistId ->
            if String.IsNullOrEmpty rawPlaylistId then
              replyToMessage "You have entered empty playlist url" |> Task.ignore
            else
              includePlaylist userId (rawPlaylistId |> Playlist.RawPlaylistId) |> Task.ignore
          | CommandWithData "/exclude" rawPlaylistId ->
            if String.IsNullOrEmpty rawPlaylistId then
              replyToMessage "You have entered empty playlist url" |> Task.ignore
            else
              excludePlaylist userId (rawPlaylistId |> Playlist.RawPlaylistId)
          | CommandWithData "/target" rawPlaylistId ->
            if String.IsNullOrEmpty rawPlaylistId then
              replyToMessage "You have entered empty playlist url" |> Task.ignore
            else
              targetPlaylist userId (rawPlaylistId |> Playlist.RawPlaylistId)
          | Equals Buttons.SetPresetSize -> askForReply Messages.SendPresetSize
          | Equals Buttons.CreatePreset -> askForReply Messages.SendPresetName
          | Equals Buttons.RunPreset -> queueCurrentPresetRun userId
          | Equals Buttons.MyPresets ->
            let sendUserPresets = Telegram.Workflows.User.listPresets sendButtons getUser
            sendUserPresets (message.From.Id |> UserId)
          | Equals Buttons.Settings -> sendSettingsMessage userId
          | Equals Buttons.IncludePlaylist -> askForReply Messages.SendIncludedPlaylist
          | Equals Buttons.ExcludePlaylist -> askForReply Messages.SendExcludedPlaylist
          | Equals Buttons.TargetPlaylist -> askForReply Messages.SendTargetedPlaylist
          | Equals "Back" -> sendCurrentPresetInfo userId

          | _ -> replyToMessage "Unknown command" |> Task.ignore
      | None ->
        match isNull message.ReplyToMessage with
        | false ->
          match (message.ReplyToMessage.Text) with
          | Equals Messages.SendIncludedPlaylist
          | Equals Messages.SendExcludedPlaylist
          | Equals Messages.SendTargetedPlaylist -> sendLoginMessage userId

          | Equals Messages.SendPresetSize ->
            let setTargetPresetSize =
              PresetSettings.setPresetSize getPreset savePreset

            let setCurrentPresetSize = User.setCurrentPresetSize getUser setTargetPresetSize

            let setTargetPresetSize =
              Workflows.User.setCurrentPresetSize sendMessage sendSettingsMessage setCurrentPresetSize

            setTargetPresetSize userId (PresetSettings.RawPresetSize message.Text)
          | Equals Messages.SendPresetName ->
            let createPreset = ((User.createPreset savePreset loadUser updateUser) |> Telegram.Workflows.User.createPreset sendButtons)

            createPreset userId message.Text
          | _ -> replyToMessage "Unknown command" |> Task.ignore
        | _ ->
          match message.Text with
          | StartsWith "/include"
          | StartsWith "/exclude"
          | StartsWith "/target"
          | Equals Buttons.IncludePlaylist
          | Equals Buttons.ExcludePlaylist
          | Equals Buttons.TargetPlaylist
          | Equals Buttons.RunPreset
          | StartsWith "/generate"
          | Equals "/start" -> sendLoginMessage userId

          | CommandWithData "/start" state ->
            let processSuccessfulLogin =
              let create = UserRepo.create _database
              let exists = UserRepo.exists _database
              let createUserIfNotExists = User.createIfNotExists exists create

              fun () ->
                task {
                  do! createUserIfNotExists userId
                  do! sendCurrentPresetInfo userId
                }

            let sendErrorMessage =
              function
              | Auth.CompleteError.StateNotFound -> replyToMessage "State not found. Try to login via fresh link."
              | Auth.CompleteError.StateDoesntBelongToUser ->
                replyToMessage "State provided does not belong to your login request. Try to login via fresh link."

            completeAuth userId state
            |> TaskResult.taskEither processSuccessfulLogin (sendErrorMessage >> Task.ignore)
          | Equals "/help" -> sendMessage Messages.Help |> Task.ignore
          | Equals "/guide" -> sendMessage Messages.Guide |> Task.ignore
          | Equals "/privacy" -> sendMessage Messages.Privacy |> Task.ignore
          | Equals "/faq" -> sendMessage Messages.FAQ |> Task.ignore
          | Equals Buttons.SetPresetSize -> askForReply Messages.SendPresetSize
          | Equals Buttons.CreatePreset -> askForReply Messages.SendPresetName
          | Equals Buttons.MyPresets ->
            let sendUserPresets = Telegram.Workflows.User.listPresets sendButtons getUser
            sendUserPresets (message.From.Id |> UserId)
          | Equals Buttons.Settings -> sendSettingsMessage userId
          | Equals "Back" -> sendCurrentPresetInfo userId

          | _ -> replyToMessage "Unknown command" |> Task.ignore)

type CallbackQueryService
  (
    _bot: ITelegramBotClient,
    _queueClient: QueueClient,
    _connectionMultiplexer: IConnectionMultiplexer,
    _database: IMongoDatabase,
    editBotMessageButtons: EditBotMessageButtons,
    telemetryClient: TelemetryClient,
    sendUserMessage: SendUserMessage,
    getPreset: Preset.Get
  ) =

  member this.ProcessAsync(callbackQuery: CallbackQuery) =
    let updatePreset = PresetRepo.save _database

    let userId = callbackQuery.From.Id |> UserId
    let botMessageId = callbackQuery.Message.MessageId |> BotMessageId

    let updateUser = UserRepo.update _database
    let editMessageButtons = editBotMessageButtons userId botMessageId
    let sendMessage = sendUserMessage userId
    let showNotification = Workflows.showNotification _bot callbackQuery.Id

    let countPlaylistTracks =
      Playlist.countTracks telemetryClient _connectionMultiplexer

    let loadUser = UserRepo.load _database
    let getUser = User.get loadUser

    let listUserPresets = Workflows.User.listPresets editMessageButtons getUser

    let sendPresetInfo = Workflows.Preset.show getPreset editMessageButtons

    let listIncludedPlaylists =
      Workflows.IncludedPlaylist.list getPreset editMessageButtons

    let listExcludedPlaylists =
      Workflows.ExcludedPlaylist.list getPreset editMessageButtons

    let listTargetedPlaylists =
      Workflows.TargetedPlaylist.list getPreset editMessageButtons

    let showIncludedPlaylist =
      Workflows.IncludedPlaylist.show editMessageButtons getPreset countPlaylistTracks

    let showExcludedPlaylist =
      Workflows.ExcludedPlaylist.show editMessageButtons getPreset countPlaylistTracks

    let showTargetedPlaylist =
      Workflows.TargetedPlaylist.show editMessageButtons getPreset countPlaylistTracks

    match callbackQuery.Data |> Workflows.parseAction with
    | Action.Preset presetAction ->
      match presetAction with
      | PresetActions.Show presetId -> sendPresetInfo presetId
      | PresetActions.Run presetId ->

        let answerCallbackQuery = Telegram.Workflows.answerCallbackQuery _bot callbackQuery.Id
        let queuePresetRun = PresetRepo.queueRun _queueClient userId
        let queuePresetRun = Domain.Workflows.Preset.queueRun getPreset Preset.validate queuePresetRun
        let queuePresetRun = Telegram.Workflows.Preset.queueRun queuePresetRun sendMessage answerCallbackQuery

        queuePresetRun presetId

    | Action.SetCurrentPreset presetId ->
      let setCurrentPreset = Domain.Workflows.User.setCurrentPreset getUser updateUser

      let setCurrentPreset =
        Workflows.User.setCurrentPreset showNotification setCurrentPreset

      setCurrentPreset userId presetId
    | Action.RemovePreset presetId ->
      let removePreset = PresetRepo.remove _database

      let removeUserPreset =
        Domain.Workflows.User.removePreset getUser removePreset updateUser

      let removeUserPreset =
        Telegram.Workflows.User.removePreset removeUserPreset listUserPresets

      removeUserPreset userId presetId
    | Action.IncludedPlaylist(IncludedPlaylistActions.Show(presetId, playlistId)) -> showIncludedPlaylist presetId playlistId
    | Action.IncludedPlaylist(IncludedPlaylistActions.List(presetId, page)) -> listIncludedPlaylists presetId page
    | Action.EnableIncludedPlaylist(presetId, playlistId) ->
      let enableIncludedPlaylist = IncludedPlaylist.enable getPreset updatePreset

      let enableIncludedPlaylist =
        Workflows.IncludedPlaylist.enable enableIncludedPlaylist showNotification showIncludedPlaylist

      enableIncludedPlaylist presetId playlistId
    | Action.DisableIncludedPlaylist(presetId, playlistId) ->
      let disableIncludedPlaylist = IncludedPlaylist.disable getPreset updatePreset

      let disableIncludedPlaylist =
        Workflows.IncludedPlaylist.disable disableIncludedPlaylist showNotification showIncludedPlaylist

      disableIncludedPlaylist presetId playlistId
    | Action.IncludedPlaylist(IncludedPlaylistActions.Remove(presetId, playlistId)) ->
      let removeIncludedPlaylist = IncludedPlaylist.remove getPreset updatePreset

      let removeIncludedPlaylist =
        Workflows.IncludedPlaylist.remove removeIncludedPlaylist showNotification listIncludedPlaylists

      removeIncludedPlaylist presetId playlistId
    | Action.ExcludedPlaylist(ExcludedPlaylistActions.List(presetId, page)) -> listExcludedPlaylists presetId page
    | Action.ExcludedPlaylist(ExcludedPlaylistActions.Show(presetId, playlistId)) -> showExcludedPlaylist presetId playlistId
    | Action.EnableExcludedPlaylist(presetId, playlistId) ->
      let enableExcludedPlaylist = ExcludedPlaylist.enable getPreset updatePreset

      let enableExcludedPlaylist =
        Workflows.ExcludedPlaylist.enable enableExcludedPlaylist showNotification showExcludedPlaylist

      enableExcludedPlaylist presetId playlistId
    | Action.DisableExcludedPlaylist(presetId, playlistId) ->
      let disableExcludedPlaylist = ExcludedPlaylist.disable getPreset updatePreset

      let disableExcludedPlaylist =
        Workflows.ExcludedPlaylist.disable disableExcludedPlaylist showNotification showExcludedPlaylist

      disableExcludedPlaylist presetId playlistId
    | Action.ExcludedPlaylist(ExcludedPlaylistActions.Remove(presetId, playlistId)) ->
      let removeExcludedPlaylist = ExcludedPlaylist.remove getPreset updatePreset

      let removeExcludedPlaylist =
        Workflows.ExcludedPlaylist.remove removeExcludedPlaylist showNotification listExcludedPlaylists

      removeExcludedPlaylist presetId playlistId
    | Action.TargetedPlaylist(TargetedPlaylistActions.List(presetId, page)) -> listTargetedPlaylists presetId page
    | Action.TargetedPlaylist(TargetedPlaylistActions.Show(presetId, playlistId)) -> showTargetedPlaylist presetId playlistId
    | Action.AppendToTargetedPlaylist(presetId, playlistId) ->
      let appendToTargetedPlaylist = TargetedPlaylist.appendTracks getPreset updatePreset

      let appendToTargetedPlaylist =
        Workflows.TargetedPlaylist.appendTracks appendToTargetedPlaylist showNotification showTargetedPlaylist

      appendToTargetedPlaylist presetId playlistId
    | Action.OverwriteTargetedPlaylist(presetId, playlistId) ->
      let overwriteTargetedPlaylist =
        TargetedPlaylist.overwriteTracks getPreset updatePreset

      let overwriteTargetedPlaylist =
        Workflows.TargetedPlaylist.overwritePlaylist overwriteTargetedPlaylist showNotification showTargetedPlaylist

      overwriteTargetedPlaylist presetId playlistId
    | Action.TargetedPlaylist(TargetedPlaylistActions.Remove(presetId, playlistId)) ->
      let removeTargetedPlaylist = TargetedPlaylist.remove getPreset updatePreset

      let removeTargetedPlaylist =
        Workflows.TargetedPlaylist.remove removeTargetedPlaylist showNotification listTargetedPlaylists

      removeTargetedPlaylist presetId playlistId
    | Action.PresetSettings(PresetSettingsActions.IncludeLikedTracks presetId) ->
      let includeLikedTracks = PresetSettings.includeLikedTracks getPreset updatePreset

      let includeLikedTracks =
        Workflows.PresetSettings.includeLikedTracks showNotification sendPresetInfo includeLikedTracks

      includeLikedTracks presetId
    | Action.PresetSettings(PresetSettingsActions.ExcludeLikedTracks presetId) ->
      let excludeLikedTracks = PresetSettings.excludeLikedTracks getPreset updatePreset

      let excludeLikedTracks =
        Workflows.PresetSettings.excludeLikedTracks showNotification sendPresetInfo excludeLikedTracks

      excludeLikedTracks presetId
    | Action.PresetSettings(PresetSettingsActions.IgnoreLikedTracks presetId) ->
      let ignoreLikedTracks = PresetSettings.ignoreLikedTracks getPreset updatePreset

      let ignoreLikedTracks =
        Workflows.PresetSettings.ignoreLikedTracks showNotification sendPresetInfo ignoreLikedTracks

      ignoreLikedTracks presetId
    | Action.PresetSettings(PresetSettingsActions.EnableRecommendations presetId) ->
      let enableRecommendations =
        PresetSettings.enableRecommendations getPreset updatePreset

      let enableRecommendations =
        Workflows.PresetSettings.enableRecommendations enableRecommendations showNotification sendPresetInfo

      enableRecommendations presetId
    | Action.PresetSettings(PresetSettingsActions.DisableRecommendations presetId) ->
      let disableRecommendations =
        PresetSettings.disableRecommendations getPreset updatePreset

      let disableRecommendations =
        Workflows.PresetSettings.disableRecommendations disableRecommendations showNotification sendPresetInfo

      disableRecommendations presetId
    | Action.PresetSettings(PresetSettingsActions.EnableUniqueArtists(presetId)) ->
      let enableUniqueArtists = PresetSettings.enableUniqueArtists getPreset updatePreset

      let enableUniqueArtists =
        Workflows.PresetSettings.enableUniqueArtists enableUniqueArtists showNotification sendPresetInfo

      enableUniqueArtists presetId
    | Action.PresetSettings(PresetSettingsActions.DisableUniqueArtists(presetId)) ->
      let disableUniqueArtists =
        PresetSettings.disableUniqueArtists getPreset updatePreset

      let disableUniqueArtists =
        Workflows.PresetSettings.disableUniqueArtists disableUniqueArtists showNotification sendPresetInfo

      disableUniqueArtists presetId
    | Action.User(UserActions.ListPresets()) -> listUserPresets userId
