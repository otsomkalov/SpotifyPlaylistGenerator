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

type AddHistoryPlaylistCommandHandler
  (
    _context: AppDbContext,
    _bot: ITelegramBotClient,
    _spotifyClientProvider: SpotifyClientProvider,
    loadCurrentPreset: User.LoadCurrentPreset
  ) =

  member this.HandleAsync replyToMessage (message: Message) =
    match message.Text with
    | CommandData data ->
      let userId = UserId message.From.Id

      task {
        let! client = _spotifyClientProvider.GetAsync message.From.Id

        let checkPlaylistExistsInSpotify = Playlist.checkPlaylistExistsInSpotify client

        let parsePlaylistId = Playlist.parseId

        let excludeInStorage = Playlist.excludeInStorage _context userId loadCurrentPreset

        let excludePlaylist =
          Playlist.excludePlaylist parsePlaylistId checkPlaylistExistsInSpotify excludeInStorage

        let rawPlaylistId = Playlist.RawPlaylistId data

        let! excludePlaylistResult = rawPlaylistId |> excludePlaylist |> Async.StartAsTask

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
    | _ -> replyToMessage "You have entered empty playlist url"
