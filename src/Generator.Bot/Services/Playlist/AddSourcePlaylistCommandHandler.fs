namespace Generator.Bot.Services.Playlist

open Resources
open System
open Database
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
    _context: AppDbContext,
    _spotifyClientProvider: SpotifyClientProvider,
    _localizer: IStringLocalizer<Messages>,
    loadCurrentPreset: User.LoadCurrentPreset
  ) =
  member this.HandleAsync replyToMessage data (message: Message) =
    let userId = UserId message.From.Id
    task {
      let! client = _spotifyClientProvider.GetAsync message.From.Id

      let checkPlaylistExistsInSpotify = Playlist.checkPlaylistExistsInSpotify client

      let parsePlaylistId = Playlist.parseId

      let includeInStorage = Playlist.includeInStorage _context userId loadCurrentPreset

      let includePlaylist =
        Playlist.includePlaylist parsePlaylistId checkPlaylistExistsInSpotify includeInStorage

      let rawPlaylistId = Playlist.RawPlaylistId data

      let! includePlaylistResult = rawPlaylistId |> includePlaylist |> Async.StartAsTask

      return!
        match includePlaylistResult with
        | Ok playlist ->
          replyToMessage $"*{playlist.Name}* successfully included\!"
        | Error error ->
          match error with
          | Playlist.IdParsing _ ->
            replyToMessage (String.Format(Messages.PlaylistIdCannotBeParsed, (rawPlaylistId |> RawPlaylistId.value)))
          | Playlist.MissingFromSpotify(Playlist.MissingFromSpotifyError id) ->
            replyToMessage (String.Format(Messages.PlaylistNotFoundInSpotify, id))
    }
