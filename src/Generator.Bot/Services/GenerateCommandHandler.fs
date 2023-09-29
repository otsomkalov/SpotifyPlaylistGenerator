namespace Generator.Bot.Services

open System
open System.Text.Json
open Azure.Storage.Queues
open Database
open Domain.Core
open Domain.Extensions
open Infrastructure.Workflows
open Shared.Services
open Telegram.Bot
open Telegram.Bot.Types
open Shared.QueueMessages
open Generator.Bot.Helpers
open Infrastructure.Core
open Domain.Workflows

type GenerateCommandHandler
  (
    _spotifyClientProvider: SpotifyClientProvider,
    _bot: ITelegramBotClient,
    _queueClient: QueueClient,
    loadUser: User.Load,
    loadPreset: Preset.Load
  ) =

  let sendSQSMessageAsync message =
    task {
      let messageJson = JsonSerializer.Serialize message

      let! _ = _queueClient.SendMessageAsync(messageJson)

      ()
    }

  let sendGenerateMessageAsync replyToMessage (message: Message) queueMessage =
    task {
      do! sendSQSMessageAsync queueMessage
      do! replyToMessage "Your playlist generation requests is queued"
    }

  let handleCommandDataAsync replyToMessage (message: Message) refreshCache currentPresetId =
    let queueMessage =
      { TelegramId = message.From.Id
        RefreshCache = refreshCache
        PresetId = currentPresetId }

    sendGenerateMessageAsync replyToMessage message queueMessage

  let handleEmptyCommandAsync replyToMessage (message: Message) currentPresetId =
    let queueMessage =
      { TelegramId = message.From.Id
        RefreshCache = false
        PresetId = currentPresetId }

    sendGenerateMessageAsync replyToMessage message queueMessage

  let validateCommandDataAsync replyToMessage (message: Message) data currentPresetId =
    match data with
    | Bool value -> handleCommandDataAsync replyToMessage message value currentPresetId
    | _ -> replyToMessage "Command data should be boolean value indicates either refresh tracks cache or not"

  let handleCommandAsync replyToMessage (message: Message) presetId =
    task {
      let presetId = presetId |> PresetId.value

      return!
        match message.Text with
        | CommandData data -> validateCommandDataAsync replyToMessage message data presetId
        | _ -> handleEmptyCommandAsync replyToMessage message presetId
    }

  let handleUserPlaylistsValidationErrorAsync replyToMessage (message: Message) errors =
    let errorsText =
        errors
        |> Seq.map (function
          | Preset.ValidationError.NoIncludedPlaylists -> "No included playlists!"
          | Preset.ValidationError.NoTargetedPlaylists -> "No target playlists!")
        |> String.concat Environment.NewLine

    replyToMessage errorsText

  member this.HandleAsync replyToMessage (message: Message) =
    task {
      let userId = message.From.Id |> UserId
      let! currentPresetId = loadUser userId |> Task.map (fun u -> u.CurrentPresetId |> Option.get)
      let! preset = loadPreset currentPresetId

      let validationResult = Preset.validate preset

      return!
        match validationResult with
        | Preset.ValidationResult.Ok -> handleCommandAsync replyToMessage message preset.Id
        | Preset.ValidationResult.Errors errors -> handleUserPlaylistsValidationErrorAsync replyToMessage message errors
    }
