namespace Generator.Bot.Services.Playlist

open Database
open Database.Entities
open Telegram.Bot
open Telegram.Bot.Types
open Microsoft.EntityFrameworkCore

type SetHistoryPlaylistCommandHandler(_playlistCommandHandler: PlaylistCommandHandler, _context: AppDbContext, _bot: ITelegramBotClient) =
  let addTargetHistoryPlaylistAsync playlistId userId =
    task {
      TargetPlaylist(Url = playlistId, UserId = userId)
      |> _context.Playlists.AddAsync
      |> ignore
    }

  let updateTargetHistoryPlaylistAsync (playlist: Playlist) playlistId =
    task {
      playlist.Url <- playlistId

      _context.Update(playlist) |> ignore

      return ()
    }

  let setTargetHistoryPlaylistAsync (message: Message) playlistId =
    task {
      let! existingTargetHistoryPlaylist =
        _context
          .TargetPlaylists
          .AsNoTracking()
          .FirstOrDefaultAsync(fun p ->
            p.UserId = message.From.Id)

      let addOrUpdateTargetHistoryPlaylistTask =
        if isNull existingTargetHistoryPlaylist then
          addTargetHistoryPlaylistAsync playlistId message.From.Id
        else
          updateTargetHistoryPlaylistAsync existingTargetHistoryPlaylist playlistId

      do! addOrUpdateTargetHistoryPlaylistTask

      let! _ = _context.SaveChangesAsync()

      _bot.SendTextMessageAsync(ChatId(message.Chat.Id), "Target history playlist successfully set!", replyToMessageId = message.MessageId)
      |> ignore
    }

  member this.HandleAsync(message: Message) =
    _playlistCommandHandler.HandleAsync message setTargetHistoryPlaylistAsync
