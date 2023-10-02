namespace Generator.Bot.Services.Playlist

open Generator.Bot
open Resources
open System
open System.Threading.Tasks
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
open Domain.Extensions

type SetTargetPlaylistCommandHandler
  (
    _bot: ITelegramBotClient,
    _spotifyClientProvider: SpotifyClientProvider,
    connectionMultiplexer: IConnectionMultiplexer,
    loadPreset: Preset.Load,
    updatePreset: Preset.Update,
    loadUser: User.Load
  ) =

  member this.HandleAsync replyToMessage data (message: Message) =
    let userId = UserId message.From.Id

    task {
      let! client = _spotifyClientProvider.GetAsync message.From.Id

      let checkPlaylistExistsInSpotify = Playlist.checkPlaylistExistsInSpotify client

      let parsePlaylistId = Playlist.parseId

      let! currentPresetId = loadUser userId |> Task.map (fun u -> u.CurrentPresetId |> Option.get)

      let targetPlaylist =
        Playlist.targetPlaylist parsePlaylistId checkPlaylistExistsInSpotify loadPreset updatePreset

      let! targetPlaylistResult = targetPlaylist currentPresetId (Playlist.RawPlaylistId data) |> Async.StartAsTask

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
            replyToMessage (String.Format(Messages.PlaylistIdCannotBeParsed, data))
          | Playlist.TargetPlaylistError.MissingFromSpotify(Playlist.MissingFromSpotifyError id) ->
            replyToMessage (String.Format(Messages.PlaylistNotFoundInSpotify, id))
          | Playlist.TargetPlaylistError.AccessError _ ->
            replyToMessage Messages.PlaylistIsReadonly
    }
