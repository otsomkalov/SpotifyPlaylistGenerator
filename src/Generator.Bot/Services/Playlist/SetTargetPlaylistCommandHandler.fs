namespace Generator.Bot.Services.Playlist

open Database
open Database.Entities
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.ReplyMarkups

type SetTargetPlaylistCommandHandler(_bot: ITelegramBotClient, _playlistCommandHandler: PlaylistCommandHandler, _context: AppDbContext) =
  let setTargetPlaylistAsync (message: Message) playlistId =
    task {
      let! _ =
        TargetPlaylist(Url = playlistId, UserId = message.From.Id, Overwrite = false)
        |> _context.TargetPlaylists.AddAsync

      let! _ = _context.SaveChangesAsync()

      let replyMarkup =
        InlineKeyboardMarkup(
          [ InlineKeyboardButton("Append", CallbackData = $"tp|{playlistId}|a")
            InlineKeyboardButton("Overwrite", CallbackData = $"tp|{playlistId}|o") ]
        )

      _bot.SendTextMessageAsync(
        ChatId(message.Chat.Id),
        "Target playlist successfully added! Do you want to overwrite or append tracks to it?",
        replyMarkup = replyMarkup,
        replyToMessageId = message.MessageId
      )
      |> ignore
    }

  member this.HandleAsync(message: Message) =
    _playlistCommandHandler.HandleAsync message setTargetPlaylistAsync
