namespace Generator.Bot.Services

open Domain.Core
open Domain.Workflows
open Resources
open Database
open Generator.Bot
open Microsoft.FSharp.Core
open Telegram.Bot
open Telegram.Bot.Types
open Helpers
open Infrastructure.Core

[<NoComparison; NoEquality>]
type SetPlaylistSizeDeps ={
  SendSettingsMessage: Telegram.SendSettingsMessage
}

type SetPlaylistSizeCommandHandler
  (
    _bot: ITelegramBotClient,
    _context: AppDbContext,
    setPlaylistSize: PresetSettings.SetPlaylistSize,
    deps: SetPlaylistSizeDeps
  ) =
  let handleWrongCommandDataAsync (message: Message) =
    task {
      _bot.SendTextMessageAsync(ChatId(message.Chat.Id), Messages.WrongPlaylistSize, replyToMessageId = message.MessageId)
      |> ignore
    }

  let setPlaylistSizeAsync size (message: Message) =
    task {
      match PlaylistSize.tryCreate size with
      | Ok playlistSize ->
        let userId = message.From.Id |> UserId

        do! setPlaylistSize userId playlistSize

        return! deps.SendSettingsMessage userId
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
