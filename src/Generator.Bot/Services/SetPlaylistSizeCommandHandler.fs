namespace Generator.Bot.Services

open System.Threading.Tasks
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
  let handleWrongCommandDataAsync replyToMessage (message: Message) : Task<unit> =
    replyToMessage Messages.WrongPlaylistSize

  let setPlaylistSizeAsync sendKeyboard replyToMessage size (message: Message) =
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
        do! replyToMessage e
    }

  member this.HandleAsync sendKeyboard replyToMessage (message: Message) =
    task {
      let processMessageFunc =
        match message.Text with
        | Int size -> setPlaylistSizeAsync sendKeyboard replyToMessage size
        | _ -> handleWrongCommandDataAsync replyToMessage

      return! processMessageFunc message
    }
