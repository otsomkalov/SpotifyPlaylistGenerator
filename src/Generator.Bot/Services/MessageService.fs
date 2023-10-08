namespace Generator.Bot.Services

open System.Threading.Tasks
open Domain.Core
open Domain.Workflows
open Infrastructure.Workflows
open Generator.Bot
open Generator.Bot.Services
open Generator.Bot.Services.Playlist
open Microsoft.FSharp.Core
open MongoDB.Driver
open Shared.Services
open Telegram.Bot
open Telegram.Bot.Types
open Generator.Bot.Helpers
open Resources
open Telegram.Bot.Types.Enums

type MessageService
  (
    _startCommandHandler: StartCommandHandler,
    _generateCommandHandler: GenerateCommandHandler,
    _addSourcePlaylistCommandHandler: AddSourcePlaylistCommandHandler,
    _addHistoryPlaylistCommandHandler: AddHistoryPlaylistCommandHandler,
    _setTargetedPlaylistCommandHandler: SetTargetPlaylistCommandHandler,
    _spotifyClientProvider: SpotifyClientProvider,
    _unauthorizedUserCommandHandler: UnauthorizedUserCommandHandler,
    _setPlaylistSizeCommandHandler: SetPlaylistSizeCommandHandler,
    _bot: ITelegramBotClient,
    loadUser: User.Load,
    loadPreset: Preset.Load,
    _database: IMongoDatabase
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

  let validateUserLogin handleCommandFunction (message: Message) =
    task{
      let! spotifyClient = _spotifyClientProvider.GetAsync message.From.Id

      return!
        if spotifyClient = null then
          _unauthorizedUserCommandHandler.HandleAsync message
        else
          handleCommandFunction message
    }

  member this.ProcessAsync(message: Message) =
    let userId = message.From.Id |> UserId

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
        | StartsWith "/start" -> _startCommandHandler.HandleAsync sendKeyboard message
        | StartsWith "/generate" -> validateUserLogin (_generateCommandHandler.HandleAsync replyToMessage) message
        | StartsWith "/include" -> validateUserLogin (includePlaylist replyToMessage) message
        | StartsWith "/exclude" -> validateUserLogin (excludePlaylist replyToMessage) message
        | StartsWith "/target" -> validateUserLogin (targetPlaylist replyToMessage) message
        | Equals Messages.SetPlaylistSize -> askForReply Messages.SendPlaylistSize
        | Equals Messages.CreatePreset -> askForReply Messages.SendPresetName
        | Equals Messages.GeneratePlaylist -> validateUserLogin (_generateCommandHandler.HandleAsync replyToMessage) message
        | Equals Messages.MyPresets -> sendUserPresets sendButtons message
        | Equals Messages.Settings -> sendSettingsMessage userId
        | Equals Messages.IncludePlaylist -> askForReply Messages.SendIncludedPlaylist
        | Equals Messages.ExcludePlaylist -> askForReply Messages.SendExcludedPlaylist
        | Equals Messages.TargetPlaylist -> askForReply Messages.SendExcludedPlaylist
        | Equals "Back" -> sendCurrentPresetInfo userId
        | _ -> replyToMessage "Unknown command"
    | _ -> Task.FromResult()
