namespace Generator.Bot.Services.Playlist

open Database
open Domain.Extensions
open Telegram
open Telegram.Bot
open Telegram.Bot.Types
open Resources
open System
open Domain.Core
open Domain.Workflows
open Generator.Bot.Services
open Infrastructure.Core
open Shared.Services
open Generator.Bot.Helpers
open Infrastructure.Workflows
open Domain.Extensions

type AddHistoryPlaylistCommandHandler
  (
    _bot: ITelegramBotClient,
    _spotifyClientProvider: SpotifyClientProvider,
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

      let excludePlaylist =
        Playlist.excludePlaylist parsePlaylistId checkPlaylistExistsInSpotify loadPreset updatePreset

      let! currentPresetId = loadUser userId |> Task.map (fun u -> u.CurrentPresetId |> Option.get)

      let rawPlaylistId = Playlist.RawPlaylistId data

      let! excludePlaylistResult = rawPlaylistId |> excludePlaylist currentPresetId

      return!
        match excludePlaylistResult with
        | Ok playlist ->
          replyToMessage $"*{playlist.Name}* successfully excluded!"
        | Error error ->
          match error with
          | Playlist.ExcludePlaylistError.IdParsing _ ->
            replyToMessage (String.Format(Messages.PlaylistIdCannotBeParsed, (rawPlaylistId |> RawPlaylistId.value)))
          | Playlist.ExcludePlaylistError.MissingFromSpotify(Playlist.MissingFromSpotifyError id) ->
            replyToMessage (String.Format(Messages.PlaylistNotFoundInSpotify, id))
    }
