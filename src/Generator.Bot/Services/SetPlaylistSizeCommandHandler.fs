namespace Generator.Bot.Services

open Domain.Core
open Domain.Workflows
open Resources
open Generator.Bot
open Microsoft.FSharp.Core
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Helpers
open Infrastructure.Core
open Domain.Extensions

type SetPlaylistSizeCommandHandler
  (
    _bot: ITelegramBotClient,
    loadUser: User.Load,
    loadPreset: Preset.Load,
    updatePreset: Preset.Update
  ) =

  let setPlaylistSizeAsync sendKeyboard replyToMessage size (message: Message) =
    let sendSettingsMessage = Telegram.Workflows.sendSettingsMessage loadUser loadPreset sendKeyboard
    let setPlaylistSize = Domain.Workflows.Preset.setPlaylistSize loadPreset updatePreset

    task {
      match PlaylistSize.tryCreate size with
      | Ok playlistSize ->
        let userId = UserId message.From.Id
        let! currentPresetId = loadUser userId |> Task.map (fun u -> u.CurrentPresetId |> Option.get)

        do! setPlaylistSize currentPresetId playlistSize

        return! sendSettingsMessage userId
      | Error e ->
        do! replyToMessage e
    }

  member this.HandleAsync sendKeyboard replyToMessage (message: Message) =
    match message.Text with
    | Int size -> setPlaylistSizeAsync sendKeyboard replyToMessage size message
    | _ -> replyToMessage Messages.WrongPlaylistSize
