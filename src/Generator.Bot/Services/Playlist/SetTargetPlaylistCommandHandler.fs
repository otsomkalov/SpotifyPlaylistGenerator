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
        TargetPlaylist(Url = playlistId, UserId = userId, Overwrite = true)
        |> _context.TargetPlaylists.AddAsync

      return ()
    }

  let setTargetPlaylistAsync (message: Message) playlistId =
    task {
      let! existingTargetPlaylist =
        _context
          .TargetPlaylists
          .AsNoTracking()
          .FirstOrDefaultAsync(fun p ->
            p.UserId = message.From.Id)

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
