module Generator.Bot.Services.MessageService

open System.Threading.Tasks
open Generator.Bot.Env
open Generator.Bot.Services
open Generator.Bot.Services.Playlist
open Telegram.Bot.Types
open Generator.Bot.Helpers
open Resources
open Shared

let private validateUserLogin handleCommandFunction (message: Message) env =
  let spotifyClient =
    Spotify.getClient message.From.Id env

  if isNull spotifyClient then
    UnauthorizedUserCommandHandler.handle message env
  else
    handleCommandFunction message env

let private getProcessReplyToMessageTextFunc (replyToMessage: Message) =
  match replyToMessage.Text with
  | Equals Messages.SendPlaylistSize -> SetPlaylistSizeCommandHandler.handleMessage

let private getProcessMessageTextFunc text : Message -> BotEnv -> Task<unit> =
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

let handle (message: Message) env =
  let handleCommandFunction =
    match isNull message.ReplyToMessage with
    | true -> getProcessMessageTextFunc message.Text
    | false -> getProcessReplyToMessageTextFunc message.ReplyToMessage

  handleCommandFunction message env
