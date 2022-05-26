module Generator.Bot.Services.Playlist.SetTargetPlaylistCommandHandler

open Database.Entities
open Shared
open Telegram.Bot.Types

let updateExistingTargetPlaylistAsync (playlist: Playlist) playlistId env =
  task {
    playlist.Url <- playlistId

    return! Db.updatePlaylist env playlist
  }

let createTargetPlaylistAsync playlistId userId env =
  Db.createPlaylist env playlistId userId PlaylistType.Target

let setTargetPlaylistAsync env (message: Message) playlistId =
  task {
    let! existingTargetPlaylist = Db.getTargetPlaylist env message.From.Id

    let addOrUpdateTargetPlaylistTask =
      if isNull existingTargetPlaylist then
        createTargetPlaylistAsync playlistId message.From.Id
      else
        updateExistingTargetPlaylistAsync existingTargetPlaylist playlistId

    do! addOrUpdateTargetPlaylistTask env

    return! Bot.replyToMessage env message.Chat.Id "Target playlist successfully set!" message.MessageId
  }

let handle env (message: Message) =
  PlaylistCommandHandler.handle env message setTargetPlaylistAsync
