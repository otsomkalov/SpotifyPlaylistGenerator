namespace Generator.Bot.Services

open Resources
open Database
open Generator.Bot
open Microsoft.FSharp.Core
open Shared.Domain
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.ReplyMarkups
open Helpers

type SetPlaylistSizeCommandHandler(_bot: ITelegramBotClient, _context: AppDbContext, _settingsCommandHandler: SettingsCommandHandler) =
  let handleWrongCommandDataAsync (message: Message) =
    task {
      _bot.SendTextMessageAsync(ChatId(message.Chat.Id), Messages.WrongPlaylistSize, replyToMessageId = message.MessageId)
      |> ignore
    }

  let setPlaylistSizeAsync size (message: Message) =
    task {
      match PlaylistSize.create size with
      | Ok playlistSize ->
        let! user = _context.Users.FindAsync message.From.Id

        user.Settings.PlaylistSize <- PlaylistSize.value playlistSize

        let! _ = _context.SaveChangesAsync()

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
