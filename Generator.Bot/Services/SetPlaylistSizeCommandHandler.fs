module Generator.Bot.Services.SetPlaylistSizeCommandHandler

open Resources
open Generator.Bot
open Microsoft.FSharp.Core
open Shared
open Shared.Domain
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.ReplyMarkups
open Helpers

let private setPlaylistSizeAsync env size (message: Message) =
  task {
    match PlaylistSize.create size with
    | Ok playlistSize ->
      let! user = Db.getUser env message.From.Id

      user.Settings.PlaylistSize <- PlaylistSize.value playlistSize

      do! Db.updateUser env user
      do! Bot.replyToMessage env message.Chat.Id Messages.PlaylistSizeSet message.MessageId

      return! SettingsCommandHandler.handle env message
    | Error error ->
      do! Bot.replyToMessage env message.Chat.Id error message.MessageId
  }

let private handleWrongCommandData env (message: Message) =
  Bot.replyToMessage env message.Chat.Id Messages.WrongPlaylistSize message.MessageId

let handleMessage env (message: Message) =
  task {
    let processMessageFunc =
      match message.Text with
      | Int size -> setPlaylistSizeAsync env size
      | _ -> handleWrongCommandData env

    return! processMessageFunc message
  }

let handleCallbackQuery env (callbackQuery: CallbackQuery) =
  task {
    do! Bot.answerCallbackQuery env callbackQuery.Id
    do! Bot.sendMessageWithMarkup env callbackQuery.From.Id Messages.SendPlaylistSize (ForceReplyMarkup())
  }

