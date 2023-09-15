namespace Generator.Bot.Services.Playlist

open Generator.Bot
open Resources
open System
open System.Threading.Tasks
open Database
open Domain.Core
open Domain.Workflows
open Generator.Bot.Services
open Infrastructure.Core
open Shared.Services
open StackExchange.Redis
open Telegram.Bot
open Telegram.Bot.Types
open Generator.Bot.Helpers
open Infrastructure.Workflows

type SetTargetPlaylistCommandHandler
  (
    _bot: ITelegramBotClient,
    _playlistCommandHandler: PlaylistCommandHandler,
    _context: AppDbContext,
    _spotifyClientProvider: SpotifyClientProvider,
    _emptyCommandDataHandler: EmptyCommandDataHandler,
    getCurrentPresetId: User.GetCurrentPresetId,
    loadPreset: Preset.Load,
    connectionMultiplexer: IConnectionMultiplexer
  ) =

  member this.HandleAsync(message: Message) =
    match message.Text with
    | CommandData data ->
      let userId = UserId message.From.Id

      async {
        let! client = _spotifyClientProvider.GetAsync message.From.Id |> Async.AwaitTask

        let checkPlaylistExistsInSpotify = Playlist.checkPlaylistExistsInSpotify client

        let parsePlaylistId = Playlist.parseId

        let targetInStorage = Playlist.targetInStorage _context userId

        let targetPlaylist =
          Playlist.targetPlaylist parsePlaylistId checkPlaylistExistsInSpotify targetInStorage

        let rawPlaylistId = Playlist.RawPlaylistId data

        let! targetPlaylistResult = rawPlaylistId |> targetPlaylist

        let! currentPresetId = getCurrentPresetId userId

        return!
          match targetPlaylistResult with
          | Ok playlist ->

            let sendMessage = Telegram.sendMessage _bot userId
            let countPlaylistTracks = Playlist.countTracks connectionMultiplexer

            let showTargetedPlaylist = Telegram.Workflows.showTargetedPlaylist sendMessage loadPreset countPlaylistTracks

            showTargetedPlaylist currentPresetId playlist.Id
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
