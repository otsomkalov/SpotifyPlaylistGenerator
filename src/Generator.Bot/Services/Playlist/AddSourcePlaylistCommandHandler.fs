namespace Generator.Bot.Services.Playlist

open Resources
open System
open System.Threading.Tasks
open Database
open Domain.Core
open Domain.Workflows
open Generator.Bot.Services
open Infrastructure.Core
open Microsoft.Extensions.Localization
open Shared.Services
open Telegram.Bot
open Telegram.Bot.Types
open Generator.Bot.Helpers
open Infrastructure.Workflows

type AddSourcePlaylistCommandHandler
  (
    _bot: ITelegramBotClient,
    _context: AppDbContext,
    _playlistCommandHandler: PlaylistCommandHandler,
    _emptyCommandDataHandler: EmptyCommandDataHandler,
    _spotifyClientProvider: SpotifyClientProvider,
    _localizer: IStringLocalizer<Messages>
  ) =
  member this.HandleAsync(message: Message) =

    match message.Text with
    | CommandData data ->
      let userId = UserId message.From.Id
      let client = _spotifyClientProvider.Get message.From.Id

      let checkPlaylistExistsInSpotify = Playlist.checkPlaylistExistsInSpotify client

      let parsePlaylistId = Playlist.parseId

      let loadCurrentPreset = User.loadCurrentPreset _context

      let includeInStorage = Playlist.includeInStorage _context userId loadCurrentPreset

      let includePlaylist =
        Playlist.includePlaylist parsePlaylistId checkPlaylistExistsInSpotify includeInStorage

      let rawPlaylistId = Playlist.RawPlaylistId data

      async {
        let! includePlaylistResult = rawPlaylistId |> includePlaylist

        return!
          match includePlaylistResult with
          | Ok _ ->
            _bot.SendTextMessageAsync(ChatId(message.Chat.Id), "Source playlist successfully added!", replyToMessageId = message.MessageId)
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
            | Playlist.MissingFromSpotify (Playlist.MissingFromSpotifyError id) ->
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
