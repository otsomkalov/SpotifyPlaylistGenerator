namespace Generator.Bot.Services

open System
open System.Text.Json
open Azure.Storage.Queues
open Database
open Domain.Core
open Infrastructure.Workflows
open Shared.Services
open Telegram.Bot
open Telegram.Bot.Types
open Shared.QueueMessages
open Generator.Bot.Helpers
open Microsoft.EntityFrameworkCore
open Infrastructure.Core
open Domain.Workflows

type GenerateCommandHandler
  (
    _spotifyClientProvider: SpotifyClientProvider,
    _bot: ITelegramBotClient,
    _context: AppDbContext,
    _queueClient: QueueClient,
    loadCurrentPreset: Domain.Workflows.User.LoadCurrentPreset
  ) =
  let handleWrongCommandDataAsync (message: Message) =
    task {
      _bot.SendTextMessageAsync(
        ChatId(message.Chat.Id),
        "Command data should be boolean value indicates either refresh tracks cache or not",
        replyToMessageId = message.MessageId
      )
      |> ignore
    }

  let sendSQSMessageAsync message =
    task {
      let messageJson = JsonSerializer.Serialize message

      let! _ = _queueClient.SendMessageAsync(messageJson)

      ()
    }

  let sendGenerateMessageAsync (message: Message) queueMessage =
    task {

      do! sendSQSMessageAsync queueMessage

      _bot.SendTextMessageAsync(
        ChatId(message.Chat.Id),
        "Your playlist generation requests is queued",
        replyToMessageId = message.MessageId
      )
      |> ignore
    }

  let handleCommandDataAsync (message: Message) refreshCache currentPresetId =
    let queueMessage =
      { TelegramId = message.From.Id
        RefreshCache = refreshCache
        PresetId = currentPresetId }

    sendGenerateMessageAsync message queueMessage

  let handleEmptyCommandAsync (message: Message) currentPresetId =
    let queueMessage =
      { TelegramId = message.From.Id
        RefreshCache = false
        PresetId = currentPresetId }

    sendGenerateMessageAsync message queueMessage

  let validateCommandDataAsync (message: Message) data currentPresetId =
    match data with
    | Bool value -> handleCommandDataAsync message value currentPresetId
    | _ -> handleWrongCommandDataAsync message

  let handleCommandAsync (message: Message) presetId =
    task {
      let presetId = presetId |> PresetId.value

      return!
        match message.Text with
        | CommandData data -> validateCommandDataAsync message data presetId
        | _ -> handleEmptyCommandAsync message presetId
    }

  let handleUserPlaylistsValidationErrorAsync (message: Message) errors =
    task {
      let errorsText =
        errors
        |> Seq.map (function
          | Preset.ValidationError.NoIncludedPlaylists -> "No included playlists!"
          | Preset.ValidationError.NoTargetPlaylists -> "No target playlists!")
        |> String.concat Environment.NewLine

      _bot.SendTextMessageAsync(ChatId(message.Chat.Id), errorsText, replyToMessageId = message.MessageId)
      |> ignore
    }

  member this.HandleAsync(message: Message) =
    task {
      let! preset = loadCurrentPreset (message.From.Id |> UserId)

      let validationResult = Preset.validate preset

      return!
        match validationResult with
        | Preset.ValidationResult.Ok -> handleCommandAsync message preset.Id
        | Preset.ValidationResult.Errors errors -> handleUserPlaylistsValidationErrorAsync message errors
    }
