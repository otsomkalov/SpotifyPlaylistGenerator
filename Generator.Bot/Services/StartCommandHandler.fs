namespace Generator.Bot.Services

open Database
open Microsoft.Extensions.Logging
open Shared.Services
open Telegram.Bot
open Telegram.Bot.Types
open Generator.Bot.Helpers
open Microsoft.EntityFrameworkCore
open Telegram.Bot.Types.ReplyMarkups
open Resources

type StartCommandHandler
  (
    _bot: ITelegramBotClient,
    _context: AppDbContext,
    _spotifyClientProvider: SpotifyClientProvider,
    _unauthorizedUserCommandHandler: UnauthorizedUserCommandHandler,
    _logger: ILogger<StartCommandHandler>
  ) =

  let sendMessageAsync (message: Message) =
    task {
      let replyMarkup =
        ReplyKeyboardMarkup(
          seq {
            seq { KeyboardButton(Messages.Settings) }
          }
        )

      _bot.SendTextMessageAsync(
        ChatId(message.Chat.Id),
        "You've successfully logged in!",
        replyToMessageId = message.MessageId,
        replyMarkup = replyMarkup
      )
      |> ignore
    }

  let setSpotifyClient (message: Message) (data: string) =
    (message.From.Id, data |> _spotifyClientProvider.Get)
    |> _spotifyClientProvider.SetClient

    sendMessageAsync message

  let handleCommandDataAsync' (message: Message) (spotifyId: string) =
    task {
      let spotifyClient =
        _spotifyClientProvider.Get message.From.Id

      return!
        if spotifyClient = null then
          setSpotifyClient message spotifyId
        else
          sendMessageAsync message
    }

  let handleCommandDataAsync (message: Message) (spotifyId: string) =
    let spotifyClient =
      _spotifyClientProvider.Get spotifyId

    if spotifyClient = null then
      _unauthorizedUserCommandHandler.HandleAsync message
    else
      handleCommandDataAsync' message spotifyId

  let createUserAsync userId =
    task {
      let! _ =
        Database.Entities.User(Id = userId)
        |> _context.Users.AddAsync

      _context.SaveChangesAsync() |> ignore
    }

  let handleEmptyCommandAsync (message: Message) =
    task {
      let! userExists =
        _context
          .Users
          .AsNoTracking()
          .AnyAsync(fun u -> u.Id = message.From.Id)

      if not userExists then
        createUserAsync message.From.Id

      return! _unauthorizedUserCommandHandler.HandleAsync message
    }

  member this.HandleAsync(message: Message) =
    match message.Text with
    | CommandData spotifyId -> handleCommandDataAsync message spotifyId
    | _ -> handleEmptyCommandAsync message
