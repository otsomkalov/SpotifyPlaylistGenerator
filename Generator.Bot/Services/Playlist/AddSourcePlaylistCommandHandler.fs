namespace Generator.Bot.Services.Playlist

open Data
open Data.Entities
open Telegram.Bot
open Telegram.Bot.Types

type AddSourcePlaylistCommandHandler(_bot: ITelegramBotClient, _context: AppDbContext, _playlistCommandHandler: PlaylistCommandHandler) =

  let addSourcePlaylistAsync (message: Message) playlistId =
    task {
      let! _ =
        Playlist(Url = playlistId, UserId = message.From.Id, PlaylistType = PlaylistType.Source)
        |> _context.Playlists.AddAsync

      let! _ = _context.SaveChangesAsync()

      _bot.SendTextMessageAsync(ChatId(message.Chat.Id), "Source playlist successfully added!", replyToMessageId = message.MessageId)
      |> ignore
    }

  member this.HandleAsync(message: Message) =
    _playlistCommandHandler.HandleAsync message addSourcePlaylistAsync
