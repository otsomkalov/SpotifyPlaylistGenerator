namespace Generator.Bot.Services

open System.Threading.Tasks
open Domain.Core
open Generator.Bot
open Generator.Bot.Services
open Generator.Bot.Services.Playlist
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
    _setTargetPlaylistCommandHandler: SetTargetPlaylistCommandHandler,
    _spotifyClientProvider: SpotifyClientProvider,
    _unauthorizedUserCommandHandler: UnauthorizedUserCommandHandler,
    _settingsCommandHandler: SettingsCommandHandler,
    _setPlaylistSizeCommandHandler: SetPlaylistSizeCommandHandler,
    sendUserPresets: Telegram.SendUserPresets,
    _emptyCommandDataHandler: EmptyCommandDataHandler,
    _bot: ITelegramBotClient
  ) =

  let sendUserPresets (message: Message) =
    sendUserPresets (message.From.Id |> UserId)

  let includePlaylist (message: Message) =
    match message.Text with
    | CommandData data -> _addSourcePlaylistCommandHandler.HandleAsync data message
    | _ -> _emptyCommandDataHandler.HandleAsync message

  let validateUserLogin handleCommandFunction (message: Message) =
    let spotifyClient = _spotifyClientProvider.Get message.From.Id

    if spotifyClient = null then
      _unauthorizedUserCommandHandler.HandleAsync message
    else
      handleCommandFunction message

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

  let getProcessReplyToMessageTextFunc (replyToMessage: Message) : (Message -> Task<unit>) =
    match replyToMessage.Text with
    | Equals Messages.SendPlaylistSize -> _setPlaylistSizeCommandHandler.HandleAsync
    | Equals Messages.SendIncludedPlaylist -> fun m -> _addSourcePlaylistCommandHandler.HandleAsync m.Text m

  let getProcessMessageTextFunc text =
    match text with
    | StartsWith "/start" -> _startCommandHandler.HandleAsync
    | StartsWith "/generate" -> validateUserLogin _generateCommandHandler.HandleAsync
    | StartsWith "/addsourceplaylist" -> validateUserLogin includePlaylist
    | StartsWith "/addhistoryplaylist" -> validateUserLogin _addHistoryPlaylistCommandHandler.HandleAsync
    | StartsWith "/settargetplaylist" -> validateUserLogin _setTargetPlaylistCommandHandler.HandleAsync
    | Equals Messages.GeneratePlaylist -> validateUserLogin _generateCommandHandler.HandleAsync
    | Equals Messages.MyPresets -> sendUserPresets
    | Equals Messages.Settings -> _settingsCommandHandler.HandleAsync
    | Equals Messages.IncludePlaylist -> askForIncludedPlaylist
    | _ -> validateUserLogin _unknownCommandHandler.HandleAsync

  let processTextMessage (message: Message) =
    let processMessageText =
      match isNull message.ReplyToMessage with
      | false -> getProcessReplyToMessageTextFunc message.ReplyToMessage
      | _ -> getProcessMessageTextFunc message.Text

    processMessageText message

  member this.ProcessAsync(message: Message) =
    let processMessage =
      match message.Type with
      | MessageType.Text -> processTextMessage
      | _ -> (fun _ -> Task.FromResult())

    processMessage message
