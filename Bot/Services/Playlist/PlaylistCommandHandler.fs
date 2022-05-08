namespace Bot.Services.Bot.Playlist

open System
open Bot.Services.Bot
open Shared.Services
open Telegram.Bot
open Telegram.Bot.Types
open Bot.Helpers

type PlaylistCommandHandler
  (
    _emptyCommandDataHandler: EmptyCommandDataHandler,
    _spotifyClientProvider: SpotifyClientProvider,
    _bot: ITelegramBotClient
  ) =

  let validatePlaylistExistsAsync (message: Message) (uri: Uri) handlePlaylistUriFunction =
    task {
      let spotifyClient =
        _spotifyClientProvider.GetClient message.From.Id

      let playlistId = uri.Segments |> Seq.last

      try
        let! _ = spotifyClient.Playlists.Get(playlistId)

        do! handlePlaylistUriFunction message playlistId
      with
      | _ ->
        let! _ =
          (ChatId(message.From.Id), "Playlist not found in Spotify or you don't have access to it.")
          |> _bot.SendTextMessageAsync

        ()
    }

  let handleNonUriCommandDataAsync (message: Message) =
    task {
      let! _ =
        (ChatId(message.From.Id), "You have entered wrong playlist url")
        |> _bot.SendTextMessageAsync

      return ()
    }

  let handleCommandDataAsync (message: Message) (data: string) handlePlaylistUriFunction =
    match Uri.TryCreate(data, UriKind.Absolute) with
    | true, uri -> validatePlaylistExistsAsync message uri handlePlaylistUriFunction
    | _ -> handleNonUriCommandDataAsync message

  member this.HandleAsync (message: Message) handlePlaylistUriFunction =
    match message.Text with
    | CommandData data -> handleCommandDataAsync message data handlePlaylistUriFunction
    | _ -> _emptyCommandDataHandler.HandleAsync message
