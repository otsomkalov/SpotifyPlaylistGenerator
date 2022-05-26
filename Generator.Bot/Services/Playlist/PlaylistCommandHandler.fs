module Generator.Bot.Services.Playlist.PlaylistCommandHandler

open System
open Generator.Bot.Services
open Shared
open Telegram.Bot.Types
open Generator.Bot.Helpers

let private validatePlaylistExistsAsync env (message: Message) (uri: Uri) handlePlaylistUriFunction =
  task {
    let spotifyClient =
      Spotify.getClient env message.From.Id

    let playlistId = uri.Segments |> Seq.last

    try
      let! _ = spotifyClient.Playlists.Get(playlistId)

      do! handlePlaylistUriFunction env message playlistId
    with
    | _ -> do! Bot.replyToMessage env message.Chat.Id "Playlist not found in Spotify or you don't have access to it." message.MessageId
  }

let handleNonUriCommandDataAsync env (message: Message) =
  Bot.replyToMessage env message.Chat.Id "You have entered wrong playlist url" message.MessageId

let handleCommandDataAsync env (message: Message) (data: string) handlePlaylistUriFunction =
  match Uri.TryCreate(data, UriKind.Absolute) with
  | true, uri -> validatePlaylistExistsAsync env message uri handlePlaylistUriFunction
  | _ -> handleNonUriCommandDataAsync env message

let handle env (message: Message) handlePlaylistUriFunction =
  match message.Text with
  | CommandData data -> handleCommandDataAsync env message data handlePlaylistUriFunction
  | _ -> EmptyCommandDataHandler.handle env message
