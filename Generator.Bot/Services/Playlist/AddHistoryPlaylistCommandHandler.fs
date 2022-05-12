namespace Generator.Bot.Services.Playlist

open Shared.Data
open Telegram.Bot
open Telegram.Bot.Types

type AddHistoryPlaylistCommandHandler(_playlistCommandHandler: PlaylistCommandHandler, _context: AppDbContext, _bot: ITelegramBotClient) =
  let addHistoryPlaylistAsync (message: Message) playlistId =
    task {
      let! _ =
        Playlist(Url = playlistId, UserId = message.From.Id, PlaylistType = PlaylistType.TargetHistory)
        |> _context.Playlists.AddAsync

      let! _ = _context.SaveChangesAsync()

      _bot.SendTextMessageAsync(ChatId(message.Chat.Id), "History playlist successfully added!", replyToMessageId = message.MessageId)
      |> ignore
    }

  member this.HandleAsync(message: Message) =
    _playlistCommandHandler.HandleAsync message addHistoryPlaylistAsync
