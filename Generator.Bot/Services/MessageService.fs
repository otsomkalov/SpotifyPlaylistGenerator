namespace Generator.Bot.Services

open Generator.Bot.Services
open Generator.Bot.Services.Playlist
open Microsoft.Extensions.Localization
open Shared.Services
open Telegram.Bot.Types
open Generator.Bot.Helpers
open Resources

type MessageService
  (
    _startCommandHandler: StartCommandHandler,
    _generateCommandHandler: GenerateCommandHandler,
    _unknownCommandHandler: UnknownCommandHandler,
    _addSourcePlaylistCommandHandler: AddSourcePlaylistCommandHandler,
    _addHistoryPlaylistCommandHandler: AddHistoryPlaylistCommandHandler,
    _setTargetPlaylistCommandHandler: SetTargetPlaylistCommandHandler,
    _setHistoryPlaylistCommandHandler: SetHistoryPlaylistCommandHandler,
    _spotifyClientProvider: SpotifyClientProvider,
    _unauthorizedUserCommandHandler: UnauthorizedUserCommandHandler,
    _settingsCommandHandler: SettingsCommandHandler
  ) =

  let validateUserLogin handleCommandFunction (message: Message) =
    let spotifyClient =
      _spotifyClientProvider.Get message.From.Id

    if spotifyClient = null then
      _unauthorizedUserCommandHandler.HandleAsync message
    else
      handleCommandFunction message

  member this.ProcessMessageAsync(message: Message) =
    let handleCommandFunction =
      match message.Text with
      | StartsWith "/start" -> _startCommandHandler.HandleAsync
      | StartsWith "/generate" -> validateUserLogin _generateCommandHandler.HandleAsync
      | StartsWith "/addsourceplaylist" -> validateUserLogin _addSourcePlaylistCommandHandler.HandleAsync
      | StartsWith "/addhistoryplaylist" -> validateUserLogin _addHistoryPlaylistCommandHandler.HandleAsync
      | StartsWith "/sethistoryplaylist" -> validateUserLogin _setHistoryPlaylistCommandHandler.HandleAsync
      | StartsWith "/settargetplaylist" -> validateUserLogin _setTargetPlaylistCommandHandler.HandleAsync
      | Equals Messages.GeneratePlaylist -> validateUserLogin _generateCommandHandler.HandleAsync
      | Equals Messages.Settings -> validateUserLogin _settingsCommandHandler.HandleAsync
      | _ -> validateUserLogin _unknownCommandHandler.HandleAsync

    handleCommandFunction message
