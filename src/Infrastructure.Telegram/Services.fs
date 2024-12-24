module Infrastructure.Telegram.Services

open System.Reflection
open FSharp
open Microsoft.ApplicationInsights
open Microsoft.Extensions.Logging
open MusicPlatform.Spotify
open MusicPlatform.Spotify.Core
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
open MusicPlatform
open otsom.fs.Core
open Infrastructure.Repos
open Domain.Repos
open otsom.fs.Bot

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
    getSpotifyClient: GetClient,
    getPreset: Preset.Get,
    validatePreset: Preset.Validate,
    sendCurrentPreset: User.SendCurrentPreset,
    parsePlaylistId: Playlist.ParseId,
    buildMusicPlatform: BuildMusicPlatform,
    buildChatContext: BuildChatContext,
    matchers: MessageHandlerMatcher seq,
    logger: ILogger<MessageService>,
    presetRepo: IPresetRepo,
    userRepo: IUserRepo,
    getUser: User.Get
  ) =

  let defaultMessageHandler (message: Telegram.Bot.Types.Message) : MessageHandler =
    let userId = message.From.Id |> UserId
    let musicPlatformUserId = message.From.Id |> string |> MusicPlatform.UserId

    let replyToMessage = replyToUserMessage userId message.MessageId
    let sendLink = Repos.sendLink _bot userId
    let sendLoginMessage = Telegram.Workflows.sendLoginMessage initAuth sendLink

    fun m ->
      let chatCtx = buildChatContext m.ChatId

      let sendSettingsMessage =
        Telegram.Workflows.User.sendCurrentPresetSettings chatCtx getUser getPreset


      task {
        let! musicPlatform = buildMusicPlatform musicPlatformUserId

        return!
          getSpotifyClient musicPlatformUserId
          |> Task.bind (function
            | Some client ->
              let includePlaylist =
                Playlist.includePlaylist musicPlatform parsePlaylistId presetRepo

              let includePlaylist =
                Workflows.CurrentPreset.includePlaylist replyToMessage getUser includePlaylist initAuth sendLink

              let excludePlaylist =
                Playlist.excludePlaylist musicPlatform parsePlaylistId presetRepo

              let excludePlaylist =
                Workflows.CurrentPreset.excludePlaylist replyToMessage getUser excludePlaylist initAuth sendLink

              let targetPlaylist =
                Playlist.targetPlaylist musicPlatform parsePlaylistId presetRepo

              let targetPlaylist =
                Workflows.CurrentPreset.targetPlaylist replyToMessage getUser targetPlaylist initAuth sendLink

              let queuePresetRun = PresetRepo.queueRun _queueClient userId

              let queuePresetRun =
                Domain.Workflows.Preset.queueRun getPreset validatePreset queuePresetRun

              let queueCurrentPresetRun =
                Workflows.User.queueCurrentPresetRun chatCtx queuePresetRun getUser (fun _ -> Task.FromResult())

              match isNull message.ReplyToMessage with
              | false ->
                match message.ReplyToMessage.Text with
                | Equals Messages.SendPresetSize ->
                  let setTargetPresetSize = PresetSettings.setPresetSize presetRepo

                  let setCurrentPresetSize = User.setCurrentPresetSize userRepo setTargetPresetSize

                  let setTargetPresetSize =
                    Workflows.User.setCurrentPresetSize sendUserMessage sendSettingsMessage setCurrentPresetSize

                  setTargetPresetSize userId (PresetSettings.RawPresetSize message.Text)
                | Equals Messages.SendIncludedPlaylist -> includePlaylist userId (Playlist.RawPlaylistId message.Text)
                | Equals Messages.SendExcludedPlaylist -> excludePlaylist userId (Playlist.RawPlaylistId message.Text)
                | Equals Messages.SendTargetedPlaylist -> targetPlaylist userId (Playlist.RawPlaylistId message.Text)
                | Equals Messages.SendPresetName ->
                  let createPreset =
                    ((User.createPreset presetRepo userRepo)
                     |> Telegram.Workflows.User.createPreset chatCtx)

                  createPreset userId message.Text
              | _ ->
                match message.Text with
                | Equals "/start" -> sendCurrentPreset userId
                | CommandWithData "/start" state ->
                  let processSuccessfulLogin =
                    let create = UserRepo.create _database
                    let exists = UserRepo.exists _database
                    let createUserIfNotExists = User.createIfNotExists exists create

                    fun () -> task {
                      do! createUserIfNotExists userId
                      do! sendCurrentPreset userId
                    }

                  let sendErrorMessage =
                    function
                    | Auth.CompleteError.StateNotFound -> replyToMessage "State not found. Try to login via fresh link."
                    | Auth.CompleteError.StateDoesntBelongToUser ->
                      replyToMessage "State provided does not belong to your login request. Try to login via fresh link."

                  completeAuth userId state
                  |> TaskResult.taskEither processSuccessfulLogin (sendErrorMessage >> Task.ignore)
                | Equals "/generate" -> queueCurrentPresetRun userId (ChatMessageId message.MessageId)
                | Equals "/version" ->
                  sendUserMessage
                    userId
                    (Assembly
                      .GetExecutingAssembly()
                      .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                      .InformationalVersion)
                  |> Task.ignore
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
                | Equals Buttons.SetPresetSize -> chatCtx.AskForReply Messages.SendPresetSize
                | Equals Buttons.CreatePreset -> chatCtx.AskForReply Messages.SendPresetName
                | Equals Buttons.RunPreset -> queueCurrentPresetRun userId (ChatMessageId message.MessageId)
                | Equals Buttons.MyPresets ->
                  let sendUserPresets = Telegram.Workflows.User.sendPresets chatCtx getUser
                  sendUserPresets (message.From.Id |> UserId)
                | Equals Buttons.IncludePlaylist -> chatCtx.AskForReply Messages.SendIncludedPlaylist
                | Equals Buttons.ExcludePlaylist -> chatCtx.AskForReply Messages.SendExcludedPlaylist
                | Equals Buttons.TargetPlaylist -> chatCtx.AskForReply Messages.SendTargetedPlaylist
                | Equals "Back" -> sendCurrentPreset userId

                | _ -> replyToMessage "Unknown command" |> Task.ignore
            | None ->
              match isNull message.ReplyToMessage with
              | false ->
                match (message.ReplyToMessage.Text) with
                | Equals Messages.SendIncludedPlaylist
                | Equals Messages.SendExcludedPlaylist
                | Equals Messages.SendTargetedPlaylist -> sendLoginMessage userId &|> ignore

                | Equals Messages.SendPresetSize ->
                  let setTargetPresetSize = PresetSettings.setPresetSize presetRepo

                  let setCurrentPresetSize = User.setCurrentPresetSize userRepo setTargetPresetSize

                  let setTargetPresetSize =
                    Workflows.User.setCurrentPresetSize sendUserMessage sendSettingsMessage setCurrentPresetSize

                  setTargetPresetSize userId (PresetSettings.RawPresetSize message.Text)
                | Equals Messages.SendPresetName ->
                  let createPreset =
                    ((User.createPreset presetRepo userRepo)
                     |> Telegram.Workflows.User.createPreset chatCtx)

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
                | Equals "/start" -> sendLoginMessage userId &|> ignore

                | CommandWithData "/start" state ->
                  let processSuccessfulLogin =
                    let create = UserRepo.create _database
                    let exists = UserRepo.exists _database
                    let createUserIfNotExists = User.createIfNotExists exists create

                    fun () -> task {
                      do! createUserIfNotExists userId
                      do! sendCurrentPreset userId
                    }

                  let sendErrorMessage =
                    function
                    | Auth.CompleteError.StateNotFound -> replyToMessage "State not found. Try to login via fresh link."
                    | Auth.CompleteError.StateDoesntBelongToUser ->
                      replyToMessage "State provided does not belong to your login request. Try to login via fresh link."

                  completeAuth userId state
                  |> TaskResult.taskEither processSuccessfulLogin (sendErrorMessage >> Task.ignore)
                | Equals Buttons.SetPresetSize -> chatCtx.AskForReply Messages.SendPresetSize
                | Equals Buttons.CreatePreset -> chatCtx.AskForReply Messages.SendPresetName
                | Equals Buttons.MyPresets ->
                  let sendUserPresets = Telegram.Workflows.User.sendPresets chatCtx getUser
                  sendUserPresets (message.From.Id |> UserId)
                | Equals "Back" -> sendCurrentPreset userId

                | _ -> replyToMessage "Unknown command" |> Task.ignore)
      }


  member this.ProcessAsync(message: Telegram.Bot.Types.Message) =
    let chatId = message.Chat.Id |> otsom.fs.Bot.ChatId

    let message' = { ChatId = chatId; Text = message.Text }

    let messageHandlers =
      matchers
      |> Seq.map (fun matcher -> matcher message')
      |> Seq.filter Option.isSome
      |> Seq.map Option.get
      |> List.ofSeq

    match messageHandlers with
    | first :: _ :: _ ->
      Logf.logfw logger "Message content matched to multiple (%i{ProcessorsCount}) handlers. Using the first." messageHandlers.Length

      first message'
    | [ handler ] -> handler message'
    | [] ->
      Logf.logfw logger "Message content didn't match any handler. Running default one."

      defaultMessageHandler message message'

type CallbackQueryService
  (
    _bot: ITelegramBotClient,
    _queueClient: QueueClient,
    _connectionMultiplexer: IConnectionMultiplexer,
    _database: IMongoDatabase,
    telemetryClient: TelemetryClient,
    getPreset: Preset.Get,
    buildChatContext: BuildChatContext,
    presetRepo: IPresetRepo,
    getUser: User.Get,
    userRepo: IUserRepo
  ) =

  member this.ProcessAsync(callbackQuery: CallbackQuery) =
    let userId = callbackQuery.From.Id |> UserId
    let chatId = callbackQuery.From.Id |> otsom.fs.Bot.ChatId

    let botMessageId =
      callbackQuery.Message.MessageId |> otsom.fs.Telegram.Bot.Core.BotMessageId

    let showNotification = Workflows.showNotification _bot callbackQuery.Id

    let countPlaylistTracks =
      Playlist.countTracks telemetryClient _connectionMultiplexer

    let chatCtx = buildChatContext chatId
    let botMessageCtx = chatCtx.BuildBotMessageContext (callbackQuery.Message.MessageId |> BotMessageId)

    let showIncludedPlaylist =
      Workflows.IncludedPlaylist.show botMessageCtx getPreset countPlaylistTracks

    let showExcludedPlaylist =
      Workflows.ExcludedPlaylist.show botMessageCtx getPreset countPlaylistTracks

    let showTargetedPlaylist =
      Workflows.TargetedPlaylist.show botMessageCtx getPreset countPlaylistTracks

    match callbackQuery.Data |> Workflows.parseAction with
    | Action.Preset presetAction ->
      match presetAction with
      | PresetActions.Show presetId ->
        let sendPresetInfo = Workflows.Preset.show getPreset botMessageCtx

        sendPresetInfo presetId
      | PresetActions.Run presetId ->

        let answerCallbackQuery = Telegram.Workflows.answerCallbackQuery _bot callbackQuery.Id
        let queuePresetRun = PresetRepo.queueRun _queueClient userId
        let queuePresetRun = Domain.Workflows.Preset.queueRun getPreset Preset.validate queuePresetRun
        let queuePresetRun = Telegram.Workflows.Preset.queueRun chatCtx queuePresetRun answerCallbackQuery

        queuePresetRun presetId

    | Action.SetCurrentPreset presetId ->
      let setCurrentPreset = Domain.Workflows.User.setCurrentPreset userRepo

      let setCurrentPreset =
        Workflows.User.setCurrentPreset showNotification setCurrentPreset

      setCurrentPreset userId presetId
    | Action.RemovePreset presetId ->
      let removePreset = PresetRepo.remove _database

      let removeUserPreset =
        Domain.Workflows.User.removePreset userRepo removePreset

      let removeUserPreset =
        Telegram.Workflows.User.removePreset botMessageCtx getUser removeUserPreset

      removeUserPreset userId presetId
    | Action.IncludedPlaylist(IncludedPlaylistActions.Show(presetId, playlistId)) -> showIncludedPlaylist presetId playlistId
    | Action.IncludedPlaylist(IncludedPlaylistActions.List(presetId, page)) ->
      let listIncludedPlaylists =
        Workflows.IncludedPlaylist.list getPreset botMessageCtx
      listIncludedPlaylists presetId page
    | Action.EnableIncludedPlaylist(presetId, playlistId) ->
      let enableIncludedPlaylist = IncludedPlaylist.enable presetRepo

      let enableIncludedPlaylist =
        Workflows.IncludedPlaylist.enable enableIncludedPlaylist showNotification showIncludedPlaylist

      enableIncludedPlaylist presetId playlistId
    | Action.DisableIncludedPlaylist(presetId, playlistId) ->
      let disableIncludedPlaylist = IncludedPlaylist.disable presetRepo

      let disableIncludedPlaylist =
        Workflows.IncludedPlaylist.disable disableIncludedPlaylist showNotification showIncludedPlaylist

      disableIncludedPlaylist presetId playlistId
    | Action.IncludedPlaylist(IncludedPlaylistActions.Remove(presetId, playlistId)) ->
      let removeIncludedPlaylist = IncludedPlaylist.remove presetRepo

      let removeIncludedPlaylist =
        Workflows.IncludedPlaylist.remove getPreset botMessageCtx removeIncludedPlaylist showNotification

      removeIncludedPlaylist presetId playlistId
    | Action.ExcludedPlaylist(ExcludedPlaylistActions.List(presetId, page)) ->
      let listExcludedPlaylists =
        Workflows.ExcludedPlaylist.list getPreset botMessageCtx
      listExcludedPlaylists presetId page
    | Action.ExcludedPlaylist(ExcludedPlaylistActions.Show(presetId, playlistId)) -> showExcludedPlaylist presetId playlistId
    | Action.EnableExcludedPlaylist(presetId, playlistId) ->
      let enableExcludedPlaylist = ExcludedPlaylist.enable presetRepo

      let enableExcludedPlaylist =
        Workflows.ExcludedPlaylist.enable enableExcludedPlaylist showNotification showExcludedPlaylist

      enableExcludedPlaylist presetId playlistId
    | Action.DisableExcludedPlaylist(presetId, playlistId) ->
      let disableExcludedPlaylist = ExcludedPlaylist.disable presetRepo

      let disableExcludedPlaylist =
        Workflows.ExcludedPlaylist.disable disableExcludedPlaylist showNotification showExcludedPlaylist

      disableExcludedPlaylist presetId playlistId
    | Action.ExcludedPlaylist(ExcludedPlaylistActions.Remove(presetId, playlistId)) ->
      let removeExcludedPlaylist = ExcludedPlaylist.remove presetRepo

      let removeExcludedPlaylist =
        Workflows.ExcludedPlaylist.remove getPreset botMessageCtx removeExcludedPlaylist showNotification

      removeExcludedPlaylist presetId playlistId
    | Action.TargetedPlaylist(TargetedPlaylistActions.List(presetId, page)) ->
      let listTargetedPlaylists =
        Workflows.TargetedPlaylist.list getPreset botMessageCtx
      listTargetedPlaylists presetId page
    | Action.TargetedPlaylist(TargetedPlaylistActions.Show(presetId, playlistId)) -> showTargetedPlaylist presetId playlistId
    | Action.AppendToTargetedPlaylist(presetId, playlistId) ->
      let appendToTargetedPlaylist = TargetedPlaylist.appendTracks presetRepo

      let appendToTargetedPlaylist =
        Workflows.TargetedPlaylist.appendTracks appendToTargetedPlaylist showNotification showTargetedPlaylist

      appendToTargetedPlaylist presetId playlistId
    | Action.OverwriteTargetedPlaylist(presetId, playlistId) ->
      let overwriteTargetedPlaylist =
        TargetedPlaylist.overwriteTracks presetRepo

      let overwriteTargetedPlaylist =
        Workflows.TargetedPlaylist.overwritePlaylist overwriteTargetedPlaylist showNotification showTargetedPlaylist

      overwriteTargetedPlaylist presetId playlistId
    | Action.TargetedPlaylist(TargetedPlaylistActions.Remove(presetId, playlistId)) ->
      let removeTargetedPlaylist = TargetedPlaylist.remove presetRepo

      let removeTargetedPlaylist =
        Workflows.TargetedPlaylist.remove getPreset botMessageCtx removeTargetedPlaylist showNotification

      removeTargetedPlaylist presetId playlistId
    | Action.PresetSettings(PresetSettingsActions.IncludeLikedTracks presetId) ->
      let includeLikedTracks = PresetSettings.includeLikedTracks presetRepo

      let includeLikedTracks =
        Workflows.PresetSettings.includeLikedTracks getPreset botMessageCtx showNotification includeLikedTracks

      includeLikedTracks presetId
    | Action.PresetSettings(PresetSettingsActions.ExcludeLikedTracks presetId) ->
      let excludeLikedTracks = PresetSettings.excludeLikedTracks presetRepo

      let excludeLikedTracks =
        Workflows.PresetSettings.excludeLikedTracks getPreset botMessageCtx showNotification excludeLikedTracks

      excludeLikedTracks presetId
    | Action.PresetSettings(PresetSettingsActions.IgnoreLikedTracks presetId) ->
      let ignoreLikedTracks = PresetSettings.ignoreLikedTracks presetRepo

      let ignoreLikedTracks =
        Workflows.PresetSettings.ignoreLikedTracks getPreset botMessageCtx showNotification ignoreLikedTracks

      ignoreLikedTracks presetId
    | Action.PresetSettings(PresetSettingsActions.EnableRecommendations presetId) ->
      let enableRecommendations =
        PresetSettings.enableRecommendations presetRepo

      let enableRecommendations =
        Workflows.PresetSettings.enableRecommendations getPreset botMessageCtx enableRecommendations showNotification

      enableRecommendations presetId
    | Action.PresetSettings(PresetSettingsActions.DisableRecommendations presetId) ->
      let disableRecommendations =
        PresetSettings.disableRecommendations presetRepo

      let disableRecommendations =
        Workflows.PresetSettings.disableRecommendations getPreset botMessageCtx disableRecommendations showNotification

      disableRecommendations presetId
    | Action.PresetSettings(PresetSettingsActions.EnableUniqueArtists(presetId)) ->
      let enableUniqueArtists = PresetSettings.enableUniqueArtists presetRepo

      let enableUniqueArtists =
        Workflows.PresetSettings.enableUniqueArtists getPreset botMessageCtx enableUniqueArtists showNotification

      enableUniqueArtists presetId
    | Action.PresetSettings(PresetSettingsActions.DisableUniqueArtists(presetId)) ->
      let disableUniqueArtists =
        PresetSettings.disableUniqueArtists presetRepo

      let disableUniqueArtists =
        Workflows.PresetSettings.disableUniqueArtists getPreset botMessageCtx disableUniqueArtists showNotification

      disableUniqueArtists presetId
    | Action.User(UserActions.ListPresets()) ->
      let listUserPresets = Workflows.User.showPresets botMessageCtx getUser
      listUserPresets userId
