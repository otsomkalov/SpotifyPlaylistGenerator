namespace Generator.Bot.Services.Playlist

open System
open Generator.Bot.Services
open Shared.Services
open Telegram.Bot
open Telegram.Bot.Types
open Generator.Bot.Helpers

type PlaylistCommandHandler
  (
    _emptyCommandDataHandler: EmptyCommandDataHandler,
    _spotifyClientProvider: SpotifyClientProvider,
    _bot: ITelegramBotClient
  ) =

  let validatePlaylistExistsAsync (message: Message) (uri: Uri) handlePlaylistUriFunction =
    task {
      let! spotifyClient =
        _spotifyClientProvider.GetAsync message.From.Id

      let playlistId = uri.Segments |> Seq.last

      try
        let! _ = spotifyClient.Playlists.Get(playlistId)

        do! handlePlaylistUriFunction message playlistId
      with
      | _ ->
        _bot.SendTextMessageAsync(
          ChatId(message.Chat.Id),
          "Playlist not found in Spotify or you don't have access to it.",
          replyToMessageId = message.MessageId
        )
        |> ignore
    }

  let handleNonUriCommandDataAsync (message: Message) =
    task {
      _bot.SendTextMessageAsync(ChatId(message.Chat.Id), "You have entered wrong playlist url", replyToMessageId = message.MessageId)
      |> ignore
    }

  let handleCommandDataAsync (message: Message) (data: string) handlePlaylistUriFunction =
    match Uri.TryCreate(data, UriKind.Absolute) with
    | true, uri -> validatePlaylistExistsAsync message uri handlePlaylistUriFunction
    | _ -> handleNonUriCommandDataAsync message

  member this.HandleAsync (message: Message) handlePlaylistUriFunction =
    match message.Text with
    | CommandData data -> handleCommandDataAsync message data handlePlaylistUriFunction
    | _ -> _emptyCommandDataHandler.HandleAsync message
