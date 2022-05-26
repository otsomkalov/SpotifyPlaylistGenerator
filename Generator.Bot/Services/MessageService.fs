module Generator.Bot.Services.MessageService

open System.Threading.Tasks
open Generator.Bot.Services
open Generator.Bot.Services.Playlist
open Telegram.Bot.Types
open Generator.Bot.Helpers
open Resources
open Shared

let private validateUserLogin handleCommandFunction env (message: Message) =
  let spotifyClient =
    Spotify.getClient env message.From.Id

  if isNull spotifyClient then
    UnauthorizedUserCommandHandler.handle env message
  else
    handleCommandFunction env message

let private getProcessReplyToMessageTextFunc (replyToMessage: Message) env =
  match replyToMessage.Text with
  | Equals Messages.SendPlaylistSize -> SetPlaylistSizeCommandHandler.handleMessage env

let private getProcessMessageTextFunc text env =
  let func =
    match text with
    | StartsWith "/start" -> StartCommandHandler.handle
    | StartsWith "/generate" -> validateUserLogin GenerateCommandHandler.handle
    | StartsWith "/addsourceplaylist" -> validateUserLogin AddSourcePlaylistCommandHandler.handle
    | StartsWith "/addhistoryplaylist" -> validateUserLogin AddHistoryPlaylistCommandHandler.handle
    | StartsWith "/sethistoryplaylist" -> validateUserLogin SetHistoryPlaylistCommandHandler.handle
    | StartsWith "/settargetplaylist" -> validateUserLogin SetTargetPlaylistCommandHandler.handle
    | Equals Messages.GeneratePlaylist -> validateUserLogin GenerateCommandHandler.handle
    | Equals Messages.Settings -> SettingsCommandHandler.handle
    | _ -> validateUserLogin UnknownCommandHandler.handle

  func env

let handle (message: Message) env =
  let handleCommandFunction =
    match isNull message.ReplyToMessage with
    | false -> getProcessReplyToMessageTextFunc message.ReplyToMessage
    | _ -> getProcessMessageTextFunc message.Text

  handleCommandFunction env message
