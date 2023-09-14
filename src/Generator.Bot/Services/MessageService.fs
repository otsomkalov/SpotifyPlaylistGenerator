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

[<NoComparison;NoEquality>]
type MessageServiceDeps ={
  SendSettingsMessage: Telegram.Core.SendSettingsMessage
  SendCurrentPresetInfo: Telegram.Core.SendCurrentPresetInfo
  AskForPlaylistSize: Telegram.Core.AskForPlaylistSize
}

type MessageService
  (
    _startCommandHandler: StartCommandHandler,
    _generateCommandHandler: GenerateCommandHandler,
    _unknownCommandHandler: UnknownCommandHandler,
    _addSourcePlaylistCommandHandler: AddSourcePlaylistCommandHandler,
    _addHistoryPlaylistCommandHandler: AddHistoryPlaylistCommandHandler,
    _setTargetPlaylistCommandHandler: SetTargetPlaylistCommandHandler,
    _spotifyClientProvider: SpotifyClientProvider,
    _unauthorizedUserCommandHandler: UnauthorizedUserCommandHandler,
    _setPlaylistSizeCommandHandler: SetPlaylistSizeCommandHandler,
    _emptyCommandDataHandler: EmptyCommandDataHandler,
    _bot: ITelegramBotClient,
    _context: AppDbContext,
    deps:MessageServiceDeps
  ) =

  let sendUserPresets sendMessage (message: Message) =
    let listPresets = Infrastructure.Workflows.User.listPresets _context

    let sendUserPresets = Telegram.Workflows.sendUserPresets sendMessage listPresets
    sendUserPresets (message.From.Id |> UserId)

  let askForPlaylistSize (message: Message) =
    deps.AskForPlaylistSize (message.From.Id |> UserId)

  let includePlaylist (message: Message) =
    match message.Text with
    | CommandData data -> _addSourcePlaylistCommandHandler.HandleAsync data message
    | _ -> _emptyCommandDataHandler.HandleAsync message

  let validateUserLogin handleCommandFunction (message: Message) =
    task{
      let! spotifyClient = _spotifyClientProvider.GetAsync message.From.Id

      return!
        if spotifyClient = null then
          _unauthorizedUserCommandHandler.HandleAsync message
        else
          handleCommandFunction message
    }

  let askForIncludedPlaylist (message: Message) =
    task {
      let! _ =
        _bot.SendTextMessageAsync(
          message.From.Id |> ChatId,
          Messages.SendIncludedPlaylist,
          replyMarkup = ForceReplyMarkup(),
          replyToMessageId = message.MessageId
        )

      ()
    }

  let sendSettingsMessage (message: Message) =
    deps.SendSettingsMessage (message.From.Id |> UserId)

  let sendCurrentPresetInfo (message: Message) =
    deps.SendCurrentPresetInfo (message.From.Id |> UserId)

  let getProcessReplyToMessageTextFunc sendMessage (replyToMessage: Message) : (Message -> Task<unit>) =
    match replyToMessage.Text with
    | Equals Messages.SendPlaylistSize -> _setPlaylistSizeCommandHandler.HandleAsync
    | Equals Messages.SendIncludedPlaylist -> fun m -> _addSourcePlaylistCommandHandler.HandleAsync m.Text m

  let getProcessMessageTextFunc sendMessage text =
    match text with
    | StartsWith "/start" -> _startCommandHandler.HandleAsync sendMessage
    | StartsWith "/generate" -> validateUserLogin _generateCommandHandler.HandleAsync
    | StartsWith "/addsourceplaylist" -> validateUserLogin includePlaylist
    | StartsWith "/addhistoryplaylist" -> validateUserLogin _addHistoryPlaylistCommandHandler.HandleAsync
    | StartsWith "/settargetplaylist" -> validateUserLogin _setTargetPlaylistCommandHandler.HandleAsync
    | Equals Messages.SetPlaylistSize -> validateUserLogin askForPlaylistSize
    | Equals Messages.GeneratePlaylist -> validateUserLogin _generateCommandHandler.HandleAsync
    | Equals Messages.MyPresets -> sendUserPresets sendMessage
    | Equals Messages.Settings -> sendSettingsMessage
    | Equals Messages.IncludePlaylist -> askForIncludedPlaylist
    | Equals "Back" -> sendCurrentPresetInfo
    | _ -> validateUserLogin _unknownCommandHandler.HandleAsync

  let processTextMessage sendMessage (message: Message) =
    let processMessageText =
      match isNull message.ReplyToMessage with
      | false -> getProcessReplyToMessageTextFunc sendMessage message.ReplyToMessage
      | _ -> getProcessMessageTextFunc sendMessage message.Text

    processMessageText message

  member this.ProcessAsync(message: Message) =

    let userId = message.From.Id |> UserId
    let sendMessage = Telegram.sendMessage _bot userId

    let processMessage =
      match message.Type with
      | MessageType.Text -> processTextMessage sendMessage
      | _ -> (fun _ -> Task.FromResult())

    processMessage message
