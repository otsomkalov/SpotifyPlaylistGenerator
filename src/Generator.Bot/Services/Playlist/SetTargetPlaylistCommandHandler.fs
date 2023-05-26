namespace Generator.Bot.Services.Playlist

open Resources
open System
open System.Threading.Tasks
open Database
open Domain.Core
open Domain.Workflows
open Generator.Bot.Services
open Infrastructure.Core
open Shared.Services
open Telegram.Bot
open Telegram.Bot.Types
open Generator.Bot.Helpers
open Infrastructure.Workflows
open Telegram.Bot.Types.ReplyMarkups

type SetTargetPlaylistCommandHandler
  (
    _bot: ITelegramBotClient,
    _playlistCommandHandler: PlaylistCommandHandler,
    _context: AppDbContext,
    _spotifyClientProvider: SpotifyClientProvider,
    _emptyCommandDataHandler: EmptyCommandDataHandler
  ) =

  member this.HandleAsync(message: Message) =
    match message.Text with
    | CommandData data ->
      let userId = UserId message.From.Id
      let client = _spotifyClientProvider.Get message.From.Id

      let checkPlaylistExistsInSpotify = Playlist.checkPlaylistExistsInSpotify client

      let parsePlaylistId = Playlist.parseId

      let targetInStorage = Playlist.targetInStorage _context userId

      let checkWriteAccess = Playlist.checkWriteAccess client

      let targetPlaylist =
        Playlist.targetPlaylist parsePlaylistId checkPlaylistExistsInSpotify checkWriteAccess targetInStorage

      let rawPlaylistId = Playlist.RawPlaylistId data

      async {
        let! targetPlaylistResult = rawPlaylistId |> targetPlaylist

        return!
          match targetPlaylistResult with
          | Ok id ->
            let id = id |> WritablePlaylistId.value |> PlaylistId.value

            let replyMarkup =
              InlineKeyboardMarkup(
                [ InlineKeyboardButton("Append", CallbackData = $"tp|{id}|a")
                  InlineKeyboardButton("Overwrite", CallbackData = $"tp|{id}|o") ]
              )

            _bot.SendTextMessageAsync(
              ChatId(message.Chat.Id),
              "Target playlist successfully added! Do you want to overwrite or append tracks to it?",
              replyMarkup = replyMarkup,
              replyToMessageId = message.MessageId
            )
            :> Task
            |> Async.AwaitTask
          | Error error ->
            match error with
            | Playlist.TargetPlaylistError.IdParsing _ ->
              _bot.SendTextMessageAsync(
                ChatId(message.Chat.Id),
                String.Format(Messages.PlaylistIdCannotBeParsed, (rawPlaylistId |> RawPlaylistId.value)),
                replyToMessageId = message.MessageId
              )
              :> Task
              |> Async.AwaitTask
            | Playlist.TargetPlaylistError.MissingFromSpotify(Playlist.MissingFromSpotifyError id) ->
              _bot.SendTextMessageAsync(
                ChatId(message.Chat.Id),
                String.Format(Messages.PlaylistNotFoundInSpotify, id),
                replyToMessageId = message.MessageId
              )
              :> Task
              |> Async.AwaitTask
            | Playlist.TargetPlaylistError.AccessError _ ->
              _bot.SendTextMessageAsync(
                ChatId(message.Chat.Id),
                Messages.PlaylistIsReadonly,
                replyToMessageId = message.MessageId
              )
              :> Task
              |> Async.AwaitTask
      }
      |> Async.StartAsTask
    | _ -> _emptyCommandDataHandler.HandleAsync message
