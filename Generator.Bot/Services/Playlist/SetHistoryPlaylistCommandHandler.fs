namespace Generator.Bot.Services.Playlist

open Shared.Data
open Telegram.Bot
open Telegram.Bot.Types
open Microsoft.EntityFrameworkCore
open EntityFrameworkCore.FSharp.DbContextHelpers

type SetHistoryPlaylistCommandHandler
  (
    _playlistCommandHandler: PlaylistCommandHandler,
    _context: AppDbContext,
    _bot: ITelegramBotClient
  ) =
  let addTargetHistoryPlaylistAsync playlistId userId =
    task {
      let! _ =
        Playlist(Url = playlistId, UserId = userId, PlaylistType = PlaylistType.TargetHistory)
        |> _context.Playlists.AddAsync

      return ()
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
        _context.Playlists.AsNoTracking()
        |> tryFilterFirstTaskAsync
             <@ fun p ->
                  p.UserId = message.From.Id
                  && p.PlaylistType = PlaylistType.TargetHistory @>

      let addOrUpdateTargetHistoryPlaylistTask =
        match existingTargetHistoryPlaylist with
        | Some p -> updateTargetHistoryPlaylistAsync p playlistId
        | None -> addTargetHistoryPlaylistAsync playlistId message.From.Id

      do! addOrUpdateTargetHistoryPlaylistTask

      let! _ = _context.SaveChangesAsync()

      let! _ =
        (ChatId(message.From.Id), "Target history playlist successfully set!")
        |> _bot.SendTextMessageAsync

      return ()
    }

  member this.HandleAsync(message: Message) =
    _playlistCommandHandler.HandleAsync message setTargetHistoryPlaylistAsync
