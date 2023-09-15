namespace Generator.Bot.Services

open Domain.Core
open Domain.Workflows
open Resources
open Database
open Generator.Bot
open Microsoft.FSharp.Core
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Helpers
open Infrastructure.Core

type SetPlaylistSizeCommandHandler
  (
    _bot: ITelegramBotClient,
    _context: AppDbContext,
    setPlaylistSize: PresetSettings.SetPlaylistSize
  ) =
  let handleWrongCommandDataAsync (message: Message) =
    task {
      _bot.SendTextMessageAsync(ChatId(message.Chat.Id), Messages.WrongPlaylistSize, replyToMessageId = message.MessageId)
      |> ignore
    }

  let setPlaylistSizeAsync sendKeyboard size (message: Message) =
    let getCurrentPresetId = Infrastructure.Workflows.User.getCurrentPresetId _context
    let loadPreset = Infrastructure.Workflows.Preset.load _context
    let getPresetMessage = Telegram.Workflows.getPresetMessage loadPreset
    let sendSettingsMessage = Telegram.Workflows.sendSettingsMessage sendKeyboard getCurrentPresetId getPresetMessage

    task {
      match PlaylistSize.tryCreate size with
      | Ok playlistSize ->
        let userId = message.From.Id |> UserId

        do! setPlaylistSize userId playlistSize

        return! sendSettingsMessage userId
      | Error e ->
        _bot.SendTextMessageAsync(ChatId(message.Chat.Id), e, replyToMessageId = message.MessageId)
        |> ignore
    }

  member this.HandleAsync sendKeyboard (message: Message) =
    task {
      let processMessageFunc =
        match message.Text with
        | Int size -> setPlaylistSizeAsync sendKeyboard size
        | _ -> handleWrongCommandDataAsync

      return! processMessageFunc message
    }
