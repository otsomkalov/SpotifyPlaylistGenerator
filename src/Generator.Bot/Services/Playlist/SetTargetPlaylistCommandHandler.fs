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
    _context: AppDbContext,
    _spotifyClientProvider: SpotifyClientProvider,
    getCurrentPresetId: User.GetCurrentPresetId,
    loadPreset: Preset.Load,
    connectionMultiplexer: IConnectionMultiplexer
  ) =

  member this.HandleAsync replyToMessage (message: Message) =
    match message.Text with
    | CommandData data ->
      let userId = UserId message.From.Id

      task {
        let! client = _spotifyClientProvider.GetAsync message.From.Id

        let checkPlaylistExistsInSpotify = Playlist.checkPlaylistExistsInSpotify client

        let parsePlaylistId = Playlist.parseId

        let targetInStorage = Playlist.targetInStorage _context userId

        let targetPlaylist =
          Playlist.targetPlaylist parsePlaylistId checkPlaylistExistsInSpotify targetInStorage

        let rawPlaylistId = Playlist.RawPlaylistId data

        let! targetPlaylistResult = rawPlaylistId |> targetPlaylist |> Async.StartAsTask

        let! currentPresetId = getCurrentPresetId userId

        return!
          match targetPlaylistResult with
          | Ok playlist ->

            let sendButtons = Telegram.sendButtons _bot userId
            let countPlaylistTracks = Playlist.countTracks connectionMultiplexer

            let showTargetedPlaylist = Telegram.Workflows.showTargetedPlaylist sendButtons loadPreset countPlaylistTracks

            showTargetedPlaylist currentPresetId playlist.Id
          | Error error ->
            match error with
            | Playlist.TargetPlaylistError.IdParsing _ ->
              replyToMessage (String.Format(Messages.PlaylistIdCannotBeParsed, (rawPlaylistId |> RawPlaylistId.value)))
            | Playlist.TargetPlaylistError.MissingFromSpotify(Playlist.MissingFromSpotifyError id) ->
              replyToMessage (String.Format(Messages.PlaylistNotFoundInSpotify, id))
            | Playlist.TargetPlaylistError.AccessError _ ->
              replyToMessage Messages.PlaylistIsReadonly
      }
    | _ -> replyToMessage "You have entered empty playlist url"
