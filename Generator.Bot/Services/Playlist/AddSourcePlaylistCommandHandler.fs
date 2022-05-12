namespace Generator.Bot.Services.Playlist

open Shared.Data
open Telegram.Bot
open Telegram.Bot.Types

type AddSourcePlaylistCommandHandler
  (
    _bot: ITelegramBotClient,
    _context: AppDbContext,
    _playlistCommandHandler: PlaylistCommandHandler
  ) =

  let addSourcePlaylistAsync (message: Message) playlistId =
    task {
      let! _ =
        Playlist(Url = playlistId, UserId = message.Chat.Id, PlaylistType = PlaylistType.Source)
        |> _context.Playlists.AddAsync

      let! _ = _context.SaveChangesAsync()

      let! _ =
        (ChatId(message.From.Id), "Source playlist successfully added!")
        |> _bot.SendTextMessageAsync

      return ()
    }

  member this.HandleAsync(message: Message) =
    _playlistCommandHandler.HandleAsync message addSourcePlaylistAsync
