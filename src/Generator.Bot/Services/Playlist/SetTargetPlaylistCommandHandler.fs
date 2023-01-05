namespace Generator.Bot.Services.Playlist

open Database
open Database.Entities
open Telegram.Bot
open Telegram.Bot.Types
open Microsoft.EntityFrameworkCore

type SetTargetPlaylistCommandHandler(_bot: ITelegramBotClient, _playlistCommandHandler: PlaylistCommandHandler, _context: AppDbContext) =
  let updateExistingTargetPlaylistAsync (playlist: Playlist) playlistId =
    task {
      playlist.Url <- playlistId

      _context.Update(playlist) |> ignore

      return ()
    }

  let createTargetPlaylistAsync playlistId userId =
    task {
      let! _ =
        Playlist(Url = playlistId, UserId = userId, PlaylistType = PlaylistType.Target)
        |> _context.Playlists.AddAsync

      return ()
    }

  let setTargetPlaylistAsync (message: Message) playlistId =
    task {
      let! existingTargetPlaylist =
        _context
          .Playlists
          .AsNoTracking()
          .FirstOrDefaultAsync(fun p ->
            p.UserId = message.From.Id
            && p.PlaylistType = PlaylistType.Target)

      let addOrUpdateTargetPlaylistTask =
        if isNull existingTargetPlaylist then
          createTargetPlaylistAsync playlistId message.From.Id
        else
          updateExistingTargetPlaylistAsync existingTargetPlaylist playlistId

      do! addOrUpdateTargetPlaylistTask

      let! _ = _context.SaveChangesAsync()

      _bot.SendTextMessageAsync(ChatId(message.Chat.Id), "Target playlist successfully set!", replyToMessageId = message.MessageId)
      |> ignore
    }

  member this.HandleAsync(message: Message) =
    _playlistCommandHandler.HandleAsync message setTargetPlaylistAsync
