namespace Generator.Bot.Services.Playlist

open Domain.Extensions
open Resources
open System
open Domain.Core
open Domain.Workflows
open Infrastructure.Core
open Microsoft.Extensions.Localization
open Shared.Services
open Telegram.Bot
open Telegram.Bot.Types
open Infrastructure.Workflows

type AddSourcePlaylistCommandHandler
  (
    _bot: ITelegramBotClient,
    _spotifyClientProvider: SpotifyClientProvider,
    _localizer: IStringLocalizer<Messages>,
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

      let includePlaylist =
        Playlist.includePlaylist parsePlaylistId checkPlaylistExistsInSpotify loadPreset updatePreset

      let rawPlaylistId = Playlist.RawPlaylistId data

      let! includePlaylistResult = rawPlaylistId |> includePlaylist currentPresetId |> Async.StartAsTask

      return!
        match includePlaylistResult with
        | Ok playlist ->
          replyToMessage $"*{playlist.Name}* successfully included!"
        | Error error ->
          match error with
          | Playlist.IdParsing _ ->
            replyToMessage (String.Format(Messages.PlaylistIdCannotBeParsed, (rawPlaylistId |> RawPlaylistId.value)))
          | Playlist.MissingFromSpotify(Playlist.MissingFromSpotifyError id) ->
            replyToMessage (String.Format(Messages.PlaylistNotFoundInSpotify, id))
    }
