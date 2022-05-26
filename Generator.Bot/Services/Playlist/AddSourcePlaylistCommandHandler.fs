module Generator.Bot.Services.Playlist.AddSourcePlaylistCommandHandler

open Database
open Database.Entities
open Shared
open Telegram.Bot
open Telegram.Bot.Types

let addSourcePlaylistAsync env (message: Message) playlistId =
  task {
    let! _ =
      Db.createPlaylist env playlistId message.From.Id PlaylistType.Source

    do! Bot.replyToMessage env message.Chat.Id "Source playlist successfully added!" message.MessageId
  }

let handle env (message: Message) =
  PlaylistCommandHandler.handle env message addSourcePlaylistAsync
