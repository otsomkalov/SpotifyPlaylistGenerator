module Generator.Bot.Services.Playlist.AddHistoryPlaylistCommandHandler

open Database.Entities
open Generator.Bot.Services.Playlist
open Shared
open Telegram.Bot.Types

let addHistoryPlaylistAsync env (message: Message) playlistId =
  task {
    do! Db.createPlaylist env playlistId message.From.Id PlaylistType.TargetHistory
    do! Bot.replyToMessage message.Chat.Id "History playlist successfully added!" message.MessageId env
  }

let handle (message: Message) env =
  PlaylistCommandHandler.handle addHistoryPlaylistAsync message env
