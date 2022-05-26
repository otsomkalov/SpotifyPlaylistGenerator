module Generator.Bot.Services.Playlist.AddHistoryPlaylistCommandHandler

open Database.Entities
open Generator.Bot.Services.Playlist
open Shared
open Telegram.Bot.Types

let addHistoryPlaylistAsync env (message: Message) playlistId =
  task {
    do! Db.createPlaylist env playlistId message.From.Id PlaylistType.TargetHistory
    do! Bot.replyToMessage env message.Chat.Id "History playlist successfully added!" message.MessageId
  }

let handle env (message: Message) =
  PlaylistCommandHandler.handle env message addHistoryPlaylistAsync
