module Generator.Bot.Services.GenerateCommandHandler

open System.Text.Json
open Database.Entities
open Shared
open Telegram.Bot.Types
open Shared.QueueMessages
open Generator.Bot.Helpers

module UserPlaylistValidation =
  let private validateHasSourcePlaylists playlistsTypes =
    match playlistsTypes
          |> Seq.tryFind (fun p -> p = PlaylistType.Source)
      with
    | Some _ -> Ok(playlistsTypes)
    | None -> Error("Source playlists are not added")

  let private validateHasTargetPlaylist playlistsTypes =
    match playlistsTypes
          |> Seq.tryFind (fun p -> p = PlaylistType.Target)
      with
    | Some _ -> Ok(playlistsTypes)
    | None -> Error("Target playlist is not set")

  let private validateHasHistoryPlaylists playlistsTypes =
    match playlistsTypes
          |> Seq.tryFind (fun p -> p = PlaylistType.History)
      with
    | Some _ -> Ok(playlistsTypes)
    | None -> Error("History playlists are not added")

  let private validateHasTargetHistoryPlaylist playlistsTypes =
    match playlistsTypes
          |> Seq.tryFind (fun p -> p = PlaylistType.TargetHistory)
      with
    | Some _ -> Ok(playlistsTypes)
    | None -> Error("Target history playlist is not set")

  let validateUserPlaylists playlistsTypes =
    (Ok playlistsTypes)
    |> Result.bind validateHasSourcePlaylists
    |> Result.bind validateHasTargetPlaylist
    |> Result.bind validateHasHistoryPlaylists
    |> Result.bind validateHasTargetHistoryPlaylist

let private handleWrongCommandDataAsync env (message: Message) =
  Bot.replyToMessage
    env
    message.Chat.Id
    "Command data should be boolean value indicates either refresh tracks cache or not"
    message.MessageId

let private sendGenerateMessageAsync env (message: Message) queueMessage =
  task {
    do! SQS.sendMessage env queueMessage
    do! Bot.replyToMessage env message.Chat.Id "Your playlist generation requests is queued" message.MessageId
  }

let private handleCommandDataAsync env (message: Message) refreshCache =
  let queueMessage =
    { TelegramId = message.From.Id
      RefreshCache = refreshCache }

  sendGenerateMessageAsync env message queueMessage

let private handleEmptyCommandAsync env (message: Message) =
  let queueMessage =
    { TelegramId = message.From.Id
      RefreshCache = false }

  sendGenerateMessageAsync env message queueMessage

let private validateCommandDataAsync env (message: Message) data =
  match data with
  | Bool value -> handleCommandDataAsync env message value
  | _ -> handleWrongCommandDataAsync env message

let private handleCommandAsync env (message: Message) =
  match message.Text with
  | CommandData data -> validateCommandDataAsync env message data
  | _ -> handleEmptyCommandAsync env message

let private validateUserPlaylistsAsync env (message: Message) =
  task {
    let! userPlaylistsTypes = Db.getUserPlaylistsTypes env message.From.Id
    return UserPlaylistValidation.validateUserPlaylists userPlaylistsTypes
  }

let private handleUserPlaylistsValidationErrorAsync env (message: Message) error =
  Bot.replyToMessage env message.Chat.Id error message.MessageId

let handle env (message: Message) =
  task {
    let! validationResult = validateUserPlaylistsAsync env message

    return!
      match validationResult with
      | Ok _ -> handleCommandAsync env message
      | Error e -> handleUserPlaylistsValidationErrorAsync env message e
  }
