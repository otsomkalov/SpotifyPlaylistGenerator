namespace Generator.Bot.Services

open System.Text.Json
open Amazon.SQS
open Microsoft.Extensions.Options
open Shared.Data
open Shared.Services
open Shared.Settings
open Telegram.Bot
open Telegram.Bot.Types
open Microsoft.EntityFrameworkCore
open System.Linq
open Shared.QueueMessages
open Generator.Bot.Helpers

module UserPlaylistValidation =
  let private validateHasSourcePlaylists playlistsTypes =
    match playlistsTypes
          |> Seq.tryFind (fun p -> p = PlaylistType.Source)
      with
    | Some _ -> Ok(playlistsTypes)
    | None -> Error("Source playlists are not added")

  let private validateHasTargetPlaylist playlistsTypes =
    match playlistsTypes
          |> Seq.tryFind (fun p -> p = PlaylistType.Target)
      with
    | Some _ -> Ok(playlistsTypes)
    | None -> Error("Target playlist is not set")

  let private validateHasHistoryPlaylists playlistsTypes =
    match playlistsTypes
          |> Seq.tryFind (fun p -> p = PlaylistType.History)
      with
    | Some _ -> Ok(playlistsTypes)
    | None -> Error("History playlists are not added")

  let private validateHasTargetHistoryPlaylist playlistsTypes =
    match playlistsTypes
          |> Seq.tryFind (fun p -> p = PlaylistType.TargetHistory)
      with
    | Some _ -> Ok(playlistsTypes)
    | None -> Error("Target history playlist is not set")

  let validateUserPlaylists playlistsTypes =
    (Ok playlistsTypes)
    |> Result.bind validateHasSourcePlaylists
    |> Result.bind validateHasTargetPlaylist
    |> Result.bind validateHasHistoryPlaylists
    |> Result.bind validateHasTargetHistoryPlaylist

type GenerateCommandHandler
  (
    _sqs: IAmazonSQS,
    _amazonOptions: IOptions<AmazonSettings>,
    _spotifyClientProvider: SpotifyClientProvider,
    _bot: ITelegramBotClient,
    _context: AppDbContext
  ) =
  let _amazonSettings = _amazonOptions.Value

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
      let messageJson =
        JsonSerializer.Serialize message

      _sqs.SendMessageAsync(_amazonSettings.QueueUrl, messageJson)
      |> ignore
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

  let handleCommandDataAsync (message: Message) refreshCache =
    task {
      let queueMessage =
        { TelegramId = message.From.Id
          RefreshCache = refreshCache }

      return! sendGenerateMessageAsync message queueMessage
    }

  let handleEmptyCommandAsync (message: Message) =
    task {
      let queueMessage =
        { TelegramId = message.From.Id
          RefreshCache = false }

      return! sendGenerateMessageAsync message queueMessage
    }

  let validateCommandDataAsync (message: Message) data =
    match data with
    | Bool value -> handleCommandDataAsync message value
    | _ -> handleWrongCommandDataAsync message

  let handleCommandAsync (message: Message) =
    match message.Text with
    | CommandData data -> validateCommandDataAsync message data
    | _ -> handleEmptyCommandAsync message

  let validateUserPlaylistsAsync (message: Message) =
    task {
      let! userPlaylistsTypes =
        _context
          .Playlists
          .AsNoTracking()
          .Where(fun p -> p.UserId = message.From.Id)
          .Select(fun p -> p.PlaylistType)
          .ToListAsync()

      return UserPlaylistValidation.validateUserPlaylists userPlaylistsTypes
    }

  let handleUserPlaylistsValidationErrorAsync (message: Message) error =
    task {
      _bot.SendTextMessageAsync(ChatId(message.Chat.Id), error, replyToMessageId = message.MessageId)
      |> ignore
    }

  member this.HandleAsync(message: Message) =
    task {
      let! validationResult = validateUserPlaylistsAsync message

      return!
        match validationResult with
        | Ok _ -> handleCommandAsync message
        | Error e -> handleUserPlaylistsValidationErrorAsync message e
    }
