namespace Generator.Bot.Services

open Domain.Core
open Resources
open Database
open Generator.Bot
open Microsoft.FSharp.Core
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.ReplyMarkups
open Helpers
open Infrastructure.Core

type SetPlaylistSizeCommandHandler(_bot: ITelegramBotClient, _context: AppDbContext, _settingsCommandHandler: SettingsCommandHandler, setPlaylistSize: UserSettings.SetPlaylistSize) =
  let handleWrongCommandDataAsync (message: Message) =
    task {
      _bot.SendTextMessageAsync(ChatId(message.Chat.Id), Messages.WrongPlaylistSize, replyToMessageId = message.MessageId)
      |> ignore
    }

  let setPlaylistSizeAsync size (message: Message) =
    task {
      match PlaylistSize.tryCreate size with
      | Ok playlistSize ->
        do! setPlaylistSize (UserId message.From.Id) playlistSize

        let! _ = _bot.SendTextMessageAsync(ChatId(message.Chat.Id), Messages.PlaylistSizeSet, replyToMessageId = message.MessageId)

        return! _settingsCommandHandler.HandleAsync message
      | Error e ->
        _bot.SendTextMessageAsync(ChatId(message.Chat.Id), e, replyToMessageId = message.MessageId)
        |> ignore
    }

  member this.HandleAsync(message: Message) =
    task {
      let processMessageFunc =
        match message.Text with
        | Int size -> setPlaylistSizeAsync size
        | _ -> handleWrongCommandDataAsync

      return! processMessageFunc message
    }

  member this.HandleAsync(callbackQuery: CallbackQuery) =
    task {
      _bot.AnswerCallbackQueryAsync(callbackQuery.Id)
      |> ignore

      _bot.SendTextMessageAsync(ChatId(callbackQuery.From.Id), Messages.SendPlaylistSize, replyMarkup = ForceReplyMarkup())
      |> ignore
    }
