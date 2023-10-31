namespace Generator.Bot.Services

open System.Threading.Tasks
open Domain.Core
open Domain.Workflows
open Infrastructure.Workflows
open Generator.Bot
open Generator.Bot.Services
open Generator.Bot.Services.Playlist
open Microsoft.Extensions.Options
open Microsoft.FSharp.Core
open MongoDB.Driver
open Shared.Services
open Shared.Settings
open StackExchange.Redis
open Telegram.Bot
open Telegram.Bot.Types
open Generator.Bot.Helpers
open Resources
open Telegram.Bot.Types.Enums
open Domain.Extensions

type MessageService
  (
    _generateCommandHandler: GenerateCommandHandler,
    _addSourcePlaylistCommandHandler: AddSourcePlaylistCommandHandler,
    _addHistoryPlaylistCommandHandler: AddHistoryPlaylistCommandHandler,
    _setTargetedPlaylistCommandHandler: SetTargetPlaylistCommandHandler,
    _spotifyClientProvider: SpotifyClientProvider,
    _setPlaylistSizeCommandHandler: SetPlaylistSizeCommandHandler,
    _bot: ITelegramBotClient,
    loadUser: User.Load,
    loadPreset: Preset.Load,
    _database: IMongoDatabase,
    _connectionMultiplexer: IConnectionMultiplexer,
    _spotifyOptions: IOptions<SpotifySettings>
  ) =

  let sendUserPresets sendMessage (message: Message) =
    let sendUserPresets = Telegram.Workflows.sendUserPresets sendMessage loadUser
    sendUserPresets (message.From.Id |> UserId)

  let includePlaylist replyToMessage (message: Message) =
    match message.Text with
    | CommandData data -> _addSourcePlaylistCommandHandler.HandleAsync replyToMessage data message
    | _ -> replyToMessage "You have entered empty playlist url"

  let excludePlaylist replyToMessage (message: Message) =
    match message.Text with
    | CommandData data -> _addHistoryPlaylistCommandHandler.HandleAsync replyToMessage data message
    | _ -> replyToMessage "You have entered empty playlist url"

  let targetPlaylist replyToMessage (message: Message) =
    match message.Text with
    | CommandData data -> _setTargetedPlaylistCommandHandler.HandleAsync replyToMessage data message
    | _ -> replyToMessage "You have entered empty playlist url"

  let validateUserLogin sendLoginMessage handleCommandFunction (message: Message) =
    task{
      let! spotifyClient = _spotifyClientProvider.GetAsync message.From.Id

      return!
        if spotifyClient = null then
          sendLoginMessage()
        else
          handleCommandFunction message
    }

  member this.ProcessAsync(message: Message) =
    let userId = message.From.Id |> UserId

    let sendMessage = Telegram.sendMessage _bot userId
    let sendLink = Telegram.sendLink _bot userId
    let sendKeyboard = Telegram.sendKeyboard _bot userId
    let replyToMessage = Telegram.replyToMessage _bot userId message.MessageId
    let sendButtons = Telegram.sendButtons _bot userId
    let askForReply = Telegram.askForReply _bot userId message.MessageId
    let savePreset = Preset.save _database
    let updateUser = User.update _database
    let createPreset = Preset.create savePreset loadUser updateUser userId

    let sendCurrentPresetInfo = Telegram.Workflows.sendCurrentPresetInfo loadUser loadPreset sendKeyboard
    let sendSettingsMessage = Telegram.Workflows.sendSettingsMessage loadUser loadPreset sendKeyboard
    let sendPresetInfo =
      Telegram.Workflows.sendPresetInfo loadPreset sendButtons
    let createPreset = Telegram.Workflows.Message.createPreset createPreset sendPresetInfo

    let sendLoginMessage () =
      let initState = Auth.initState _connectionMultiplexer
      let getLoginLink = Auth.getLoginLink _spotifyOptions

      let getLoginLink = Domain.Workflows.Auth.getLoginLink initState getLoginLink

      getLoginLink userId
      |> Task.bind (sendLink Messages.LoginToSpotify Messages.Login)

    match message.Type with
    | MessageType.Text ->
      match isNull message.ReplyToMessage with
      | false ->
        match message.ReplyToMessage.Text with
        | Equals Messages.SendPlaylistSize -> _setPlaylistSizeCommandHandler.HandleAsync sendKeyboard replyToMessage message
        | Equals Messages.SendIncludedPlaylist -> _addSourcePlaylistCommandHandler.HandleAsync replyToMessage message.Text message
        | Equals Messages.SendExcludedPlaylist -> _addHistoryPlaylistCommandHandler.HandleAsync replyToMessage message.Text message
        | Equals Messages.SendPresetName -> createPreset message.Text
      | _ ->
        match message.Text with
        | Equals "/start" ->
          sendLoginMessage()
        | CommandWithData "/start" state ->
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

          let completeAuth = Domain.Workflows.Auth.complete tryGetAuth getToken saveCompletedAuth createUserIfNotExists

          completeAuth userId (state |> Auth.State.parse)
          |> Task.bind (Result.either (fun () -> sendCurrentPresetInfo userId) sendErrorMessage)
        | Equals "/help" ->
          sendMessage Messages.Help
        | Equals "/guide" -> sendMessage Messages.Guide
        | Equals "/privacy" -> sendMessage Messages.Privacy
        | Equals "/faq" -> sendMessage Messages.FAQ
        | StartsWith "/generate" -> validateUserLogin sendLoginMessage (_generateCommandHandler.HandleAsync replyToMessage) message
        | StartsWith "/include" -> validateUserLogin sendLoginMessage (includePlaylist replyToMessage) message
        | StartsWith "/exclude" -> validateUserLogin sendLoginMessage (excludePlaylist replyToMessage) message
        | StartsWith "/target" -> validateUserLogin sendLoginMessage (targetPlaylist replyToMessage) message
        | Equals Buttons.SetPlaylistSize -> askForReply Messages.SendPlaylistSize
        | Equals Buttons.CreatePreset -> askForReply Messages.SendPresetName
        | Equals Buttons.GeneratePlaylist -> validateUserLogin sendLoginMessage (_generateCommandHandler.HandleAsync replyToMessage) message
        | Equals Buttons.MyPresets -> sendUserPresets sendButtons message
        | Equals Buttons.Settings -> sendSettingsMessage userId
        | Equals Buttons.IncludePlaylist -> askForReply Messages.SendIncludedPlaylist
        | Equals Buttons.ExcludePlaylist -> askForReply Messages.SendExcludedPlaylist
        | Equals Buttons.TargetPlaylist -> askForReply Messages.SendExcludedPlaylist
        | Equals "Back" -> sendCurrentPresetInfo userId
        | _ -> replyToMessage "Unknown command"
    | _ -> Task.FromResult()
