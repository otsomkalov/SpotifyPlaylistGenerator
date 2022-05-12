namespace Generator.Bot.Services.Playlist

open Shared.Data
open Telegram.Bot
open Telegram.Bot.Types

type AddHistoryPlaylistCommandHandler
  (
    _playlistCommandHandler: PlaylistCommandHandler,
    _context: AppDbContext,
    _bot: ITelegramBotClient
  ) =
  let addHistoryPlaylistAsync (message: Message) playlistId =
    task {
      let! _ =
        Playlist(Url = playlistId, UserId = message.Chat.Id, PlaylistType = PlaylistType.TargetHistory)
        |> _context.Playlists.AddAsync

      let! _ = _context.SaveChangesAsync()

      let! _ =
        (ChatId(message.From.Id), "History playlist successfully added!")
        |> _bot.SendTextMessageAsync

      return ()
    }

  member this.HandleAsync(message: Message) =
    _playlistCommandHandler.HandleAsync message addHistoryPlaylistAsync
