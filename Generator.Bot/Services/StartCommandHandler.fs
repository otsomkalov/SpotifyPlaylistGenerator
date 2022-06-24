module Generator.Bot.Services.StartCommandHandler

open Microsoft.FSharp.Core
open Shared
open Telegram.Bot.Types
open Generator.Bot.Helpers

module private CommandDataHandler =
  let handle (spotifyId: string) (message: Message) env =
    let spotifyClientBySpotifyId =
      Spotify.getClientBySpotifyId env spotifyId

    if isNull spotifyClientBySpotifyId then
      UnauthorizedUserCommandHandler.handle message env
    else
      task { Spotify.setClient env message.From.Id spotifyClientBySpotifyId }

let handle (message: Message) env =
  let processMessageFunc =
    match message.Text with
    | CommandWithData spotifyId -> CommandDataHandler.handle spotifyId
    | _ -> SettingsCommandHandler.handle

  processMessageFunc message env
