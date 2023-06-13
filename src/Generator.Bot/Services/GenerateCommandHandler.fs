namespace Generator.Bot.Services

open System
open System.Text.Json
open Azure.Storage.Queues
open Database
open Domain.Core
open Shared.Services
open Telegram.Bot
open Telegram.Bot.Types
open Shared.QueueMessages
open Generator.Bot.Helpers
open Microsoft.EntityFrameworkCore
open Domain.Extensions
open Infrastructure.Core

type GenerateCommandHandler
  (
    _spotifyClientProvider: SpotifyClientProvider,
    _bot: ITelegramBotClient,
    _context: AppDbContext,
    _queueClient: QueueClient,
    validateUserPlaylists: ValidateUserPlaylists.Action,
    getCurrentPresetId: Domain.Workflows.User.GetCurrentPresetId
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

  let handleCommandAsync (message: Message) =
    task {
      let userId = message.From.Id |> UserId

      let! currentPresetId = getCurrentPresetId userId |> Async.map PresetId.value |> Async.StartAsTask

      return!
        match message.Text with
        | CommandData data -> validateCommandDataAsync message data currentPresetId
        | _ -> handleEmptyCommandAsync message currentPresetId
    }

  let handleUserPlaylistsValidationErrorAsync (message: Message) errors =
    task {
      let errorsText =
        errors
        |> Seq.map (function
          | ValidateUserPlaylists.NoIncludedPlaylists -> "No included playlists!"
          | ValidateUserPlaylists.NoTargetPlaylists -> "No target playlists!")
        |> String.concat Environment.NewLine

      _bot.SendTextMessageAsync(ChatId(message.Chat.Id), errorsText, replyToMessageId = message.MessageId)
      |> ignore
    }

  member this.HandleAsync(message: Message) =
    task {
      let! validationResult = validateUserPlaylists (message.From.Id |> UserId)

      return!
        match validationResult with
        | ValidateUserPlaylists.Ok -> handleCommandAsync message
        | ValidateUserPlaylists.Errors errors -> handleUserPlaylistsValidationErrorAsync message errors
    }
