module Generator.Bot.Services.Playlist.SetHistoryPlaylistCommandHandler

open Database.Entities
open Shared
open Telegram.Bot.Types

let addTargetHistoryPlaylistAsync playlistId userId env =
  Db.createPlaylist env playlistId userId PlaylistType.TargetHistory

let updateTargetHistoryPlaylistAsync (playlist: Playlist) playlistId env =
  task {
    playlist.Url <- playlistId

    return! Db.updatePlaylist env playlist
  }

let setTargetHistoryPlaylistAsync env (message: Message) playlistId =
  task {
    let! existingTargetHistoryPlaylist = Db.getTargetHistoryPlaylist env message.From.Id

    let addOrUpdateTargetHistoryPlaylistTask =
      if isNull existingTargetHistoryPlaylist then
        addTargetHistoryPlaylistAsync playlistId message.From.Id
      else
        updateTargetHistoryPlaylistAsync existingTargetHistoryPlaylist playlistId

    do! addOrUpdateTargetHistoryPlaylistTask env

    return! Bot.replyToMessage env message.Chat.Id "Target history playlist successfully set!" message.MessageId
  }

let handle env (message: Message) =
  PlaylistCommandHandler.handle env message setTargetHistoryPlaylistAsync
