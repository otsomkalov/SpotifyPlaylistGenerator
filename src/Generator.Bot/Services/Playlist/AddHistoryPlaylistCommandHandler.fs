namespace Generator.Bot.Services.Playlist

open Database
open Domain.Extensions
open Telegram
open Telegram.Bot
open Telegram.Bot.Types
open Resources
open System
open System.Threading.Tasks
open Domain.Core
open Domain.Workflows
open Generator.Bot.Services
open Infrastructure.Core
open Shared.Services
open Generator.Bot.Helpers
open Infrastructure.Workflows
open Telegram.Bot.Types.Enums
open Telegram.Bot.Types.ReplyMarkups

type AddHistoryPlaylistCommandHandler
  (
    _playlistCommandHandler: PlaylistCommandHandler,
    _context: AppDbContext,
    _bot: ITelegramBotClient,
    _spotifyClientProvider: SpotifyClientProvider,
    _emptyCommandDataHandler: EmptyCommandDataHandler,
    loadCurrentPreset: User.LoadCurrentPreset
  ) =

  member this.HandleAsync(message: Message) =
    match message.Text with
    | CommandData data ->
      let userId = UserId message.From.Id

      async {
        let! client = _spotifyClientProvider.GetAsync message.From.Id |> Async.AwaitTask

        let checkPlaylistExistsInSpotify = Playlist.checkPlaylistExistsInSpotify client

        let parsePlaylistId = Playlist.parseId

        let includeInStorage = Playlist.excludeInStorage _context userId loadCurrentPreset

        let includePlaylist =
          Playlist.includePlaylist parsePlaylistId checkPlaylistExistsInSpotify includeInStorage

        let rawPlaylistId = Playlist.RawPlaylistId data

        let! excludePlaylistResult = rawPlaylistId |> includePlaylist

        return!
          match excludePlaylistResult with
          | Ok playlist ->
            _bot.SendTextMessageAsync(
              ChatId(message.Chat.Id),
              $"*{playlist.Name |> Workflows.escapeMarkdownString}* successfully excluded\!",
              ParseMode.MarkdownV2,
              replyToMessageId = message.MessageId
            )
            :> Task
            |> Async.AwaitTask
          | Error error ->
            match error with
            | Playlist.IdParsing _ ->
              _bot.SendTextMessageAsync(
                ChatId(message.Chat.Id),
                String.Format(Messages.PlaylistIdCannotBeParsed, (rawPlaylistId |> RawPlaylistId.value)),
                replyToMessageId = message.MessageId
              )
              :> Task
              |> Async.AwaitTask
            | Playlist.MissingFromSpotify(Playlist.MissingFromSpotifyError id) ->
              _bot.SendTextMessageAsync(
                ChatId(message.Chat.Id),
                String.Format(Messages.PlaylistNotFoundInSpotify, id),
                replyToMessageId = message.MessageId
              )
              :> Task
              |> Async.AwaitTask
      }
      |> Async.StartAsTask
    | _ -> _emptyCommandDataHandler.HandleAsync message
