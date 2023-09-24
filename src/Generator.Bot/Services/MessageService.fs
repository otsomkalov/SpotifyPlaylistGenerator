namespace Generator.Bot.Services

open System.Threading.Tasks
open Database
open Domain.Core
open Generator.Bot
open Generator.Bot.Services
open Generator.Bot.Services.Playlist
open Microsoft.FSharp.Core
open Shared.Services
open Telegram.Bot
open Telegram.Bot.Types
open Generator.Bot.Helpers
open Resources
open Telegram.Bot.Types.Enums
open Telegram.Bot.Types.ReplyMarkups

type MessageService
  (
    _startCommandHandler: StartCommandHandler,
    _generateCommandHandler: GenerateCommandHandler,
    _unknownCommandHandler: UnknownCommandHandler,
    _addSourcePlaylistCommandHandler: AddSourcePlaylistCommandHandler,
    _addHistoryPlaylistCommandHandler: AddHistoryPlaylistCommandHandler,
    _setTargetedPlaylistCommandHandler: SetTargetPlaylistCommandHandler,
    _spotifyClientProvider: SpotifyClientProvider,
    _unauthorizedUserCommandHandler: UnauthorizedUserCommandHandler,
    _setPlaylistSizeCommandHandler: SetPlaylistSizeCommandHandler,
    _bot: ITelegramBotClient,
    _context: AppDbContext
  ) =

  let sendUserPresets sendMessage (message: Message) =
    let listPresets = Infrastructure.Workflows.User.listPresets _context

    let sendUserPresets = Telegram.Workflows.sendUserPresets sendMessage listPresets
    sendUserPresets (message.From.Id |> UserId)

  let includePlaylist replyToMessage (message: Message) =
    match message.Text with
    | CommandData data -> _addSourcePlaylistCommandHandler.HandleAsync replyToMessage data message
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

  let sendSettingsMessage sendKeyboard (message: Message) =
    let getCurrentPresetId = Infrastructure.Workflows.User.getCurrentPresetId _context
    let loadPreset = Infrastructure.Workflows.Preset.load _context
    let getPresetMessage = Telegram.Workflows.getPresetMessage loadPreset
    let sendSettingsMessage = Telegram.Workflows.sendSettingsMessage sendKeyboard getCurrentPresetId getPresetMessage

    sendSettingsMessage (message.From.Id |> UserId)

  let sendCurrentPresetInfo sendKeyboard (message: Message) =
    let getCurrentPresetId = Infrastructure.Workflows.User.getCurrentPresetId _context
    let loadPreset = Infrastructure.Workflows.Preset.load _context
    let getPresetMessage = Telegram.Workflows.getPresetMessage loadPreset
    let sendCurrentPresetInfo = Telegram.Workflows.sendCurrentPresetInfo sendKeyboard getCurrentPresetId getPresetMessage

    sendCurrentPresetInfo (message.From.Id |> UserId)

  let getProcessReplyToMessageTextFunc sendKeyboard sendMessage replyToMessage (repliedMessage: Message) : (Message -> Task<unit>) =
    match repliedMessage.Text with
    | Equals Messages.SendPlaylistSize -> _setPlaylistSizeCommandHandler.HandleAsync sendKeyboard replyToMessage
    | Equals Messages.SendIncludedPlaylist -> fun m -> _addSourcePlaylistCommandHandler.HandleAsync replyToMessage m.Text m

  let getProcessMessageTextFunc sendKeyboard sendMessage sendButtons replyToMessage askForReply text =
    match text with
    | StartsWith "/start" -> _startCommandHandler.HandleAsync sendKeyboard
    | StartsWith "/generate" -> validateUserLogin (_generateCommandHandler.HandleAsync replyToMessage)
    | StartsWith "/addsourceplaylist" -> validateUserLogin (includePlaylist replyToMessage)
    | StartsWith "/addhistoryplaylist" -> validateUserLogin (_addHistoryPlaylistCommandHandler.HandleAsync replyToMessage)
    | StartsWith "/settargetplaylist" -> validateUserLogin (_setTargetedPlaylistCommandHandler.HandleAsync replyToMessage)
    | Equals Messages.SetPlaylistSize -> validateUserLogin (fun m -> askForReply Messages.SendPlaylistSize)
    | Equals Messages.GeneratePlaylist -> validateUserLogin (_generateCommandHandler.HandleAsync replyToMessage)
    | Equals Messages.MyPresets -> sendUserPresets sendButtons
    | Equals Messages.Settings -> (sendSettingsMessage sendKeyboard)
    | Equals Messages.IncludePlaylist -> (fun m -> askForReply Messages.SendIncludedPlaylist)
    | Equals "Back" -> (sendCurrentPresetInfo sendKeyboard)
    | _ -> validateUserLogin (_unknownCommandHandler.HandleAsync replyToMessage)

  let processTextMessage sendKeyboard sendMessage sendButtons replyToMessage askForReply (message: Message) =
    let processMessageText =
      match isNull message.ReplyToMessage with
      | false -> getProcessReplyToMessageTextFunc sendKeyboard sendMessage replyToMessage message.ReplyToMessage
      | _ -> getProcessMessageTextFunc sendKeyboard sendMessage sendButtons replyToMessage askForReply message.Text

    processMessageText message

  member this.ProcessAsync(message: Message) =

    let userId = message.From.Id |> UserId
    let sendMessage = Telegram.sendMessage _bot userId
    let sendKeyboard = Telegram.sendKeyboard _bot userId
    let replyToMessage = Telegram.replyToMessage _bot userId message.MessageId
    let sendButtons = Telegram.sendButtons _bot userId
    let askForReply = Telegram.askForReply _bot userId message.MessageId

    let processMessage =
      match message.Type with
      | MessageType.Text -> processTextMessage sendKeyboard sendMessage sendButtons replyToMessage askForReply
      | _ -> (fun _ -> Task.FromResult())

    processMessage message
