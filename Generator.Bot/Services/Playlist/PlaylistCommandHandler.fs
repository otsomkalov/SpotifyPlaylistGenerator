module Generator.Bot.Services.Playlist.PlaylistCommandHandler

open System
open System.Threading.Tasks
open Generator.Bot.Env
open Shared
open Telegram.Bot.Types
open Generator.Bot.Helpers

module private EmptyCommandDataHandler =
  let handle (message: Message) env =
    Bot.replyToMessage message.Chat.Id "You have entered empty playlist url" message.MessageId env

let private validatePlaylistExistsAsync (uri: Uri) handlePlaylistUriFunction (message: Message) env =
  task {
    let spotifyClient =
      Spotify.getClient message.From.Id env

    let playlistId = uri.Segments |> Seq.last

    try
      let! _ = spotifyClient.Playlists.Get(playlistId)

      do! handlePlaylistUriFunction env message playlistId
    with
    | _ -> do! Bot.replyToMessage message.Chat.Id "Playlist not found in Spotify or you don't have access to it." message.MessageId env
  }

let handleNonUriCommandDataAsync (message: Message) env =
  Bot.replyToMessage message.Chat.Id "You have entered wrong playlist url" message.MessageId env

let handleCommandDataAsync (data: string) handlePlaylistUriFunction (message: Message) env : Task<unit> =
  let f =
    match Uri.TryCreate(data, UriKind.Absolute) with
    | true, uri -> validatePlaylistExistsAsync uri handlePlaylistUriFunction
    | _ -> handleNonUriCommandDataAsync

  f message env

let handle handlePlaylistUriFunction (message: Message) env : Task<unit> =
  let f: Message -> BotEnv -> Task<unit> =
    match message.Text with
    | CommandWithData data -> handleCommandDataAsync data handlePlaylistUriFunction
    | _ -> EmptyCommandDataHandler.handle

  f message env
