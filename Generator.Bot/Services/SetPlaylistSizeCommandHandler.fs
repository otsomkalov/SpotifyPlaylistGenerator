module Generator.Bot.Services.SetPlaylistSizeCommandHandler

open System.Threading.Tasks
open Generator.Bot.Env
open Resources
open Generator.Bot
open Microsoft.FSharp.Core
open Shared
open Shared.Domain
open Shared.Domain.PlaylistSize
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.ReplyMarkups
open Helpers

let private setPlaylistSize playlistSize (message:  Message) env : PlaylistSize -> Message -> BotEnv -> Task<unit> =
  task{
    let! user = Db.getUser env message.From.Id

    user.Settings.PlaylistSize <- PlaylistSize.value playlistSize

    do! Db.updateUser env user
    do! Bot.replyToMessage message.Chat.Id Messages.PlaylistSizeSet message.MessageId env

    return SettingsCommandHandler.handle message
  }

let private setPlaylistSizeAsync size (message: Message) env : Task<BotEnv -> Task<unit>> =
  task {
    match PlaylistSize.create size with
    | Ok playlistSize -> setPlaylistSize playlistSize message
    | Error error ->
      return Bot.replyToMessage message.Chat.Id error message.MessageId
  }

let private handleWrongCommandData (message: Message) =
  Bot.replyToMessage message.Chat.Id Messages.WrongPlaylistSize message.MessageId

let handleMessage (message: Message) : BotEnv -> Task<unit> =
  let f =
    match message.Text with
    | Int size -> setPlaylistSizeAsync size
    | _ -> handleWrongCommandData

  f message

let handleCallbackQuery (callbackQuery: CallbackQuery) env =
  task {
    do! Bot.answerCallbackQuery env callbackQuery.Id
    return! Bot.sendMessageWithMarkup callbackQuery.From.Id Messages.SendPlaylistSize (ForceReplyMarkup()) env
  }

