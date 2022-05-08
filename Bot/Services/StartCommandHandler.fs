namespace Bot.Services.Bot

open Bot.Services
open Microsoft.Extensions.Logging
open Shared.Data
open Shared.Services
open Telegram.Bot
open Telegram.Bot.Types
open Bot.Helpers
open Microsoft.EntityFrameworkCore
open Shared.QueueMessages

type StartCommandHandler
  (
    _bot: ITelegramBotClient,
    _context: AppDbContext,
    _spotifyClientProvider: SpotifyClientProvider,
    _unauthorizedUserCommandHandler: UnauthorizedUserCommandHandler,
    _logger: ILogger<StartCommandHandler>,
    _sqsService: SQSService,
    _spotifyIdProvider: SpotifyIdProvider
  ) =

  let sendMessageAsync (message: Message) (data: string) =
    task {
      (message.From.Id, data |> _spotifyClientProvider.GetClient)
      |> _spotifyClientProvider.SetClient

      let linkAccountsMessage =
        LinkAccountsMessage(SpotifyId = data, TelegramId = message.From.Id)

      do! _sqsService.SendMessageAsync linkAccountsMessage MessageTypes.LinkAccounts

      _spotifyIdProvider.Set message.From.Id data

      let! _ =
        (ChatId(message.From.Id), "You've successfully logged in!")
        |> _bot.SendTextMessageAsync

      ()
    }

  let addUserAndSendMessageAsync (message: Message) data =
    task {
      let! _ =
        Shared.Data.User(SpotifyId = data, Id = message.From.Id)
        |> _context.Users.AddAsync

      let! _ = _context.SaveChangesAsync()

      return! sendMessageAsync message data
    }

  let handleCommandDataAsync (message: Message) data =
    task {
      let! userExists =
        _context
          .Users
          .AsNoTracking()
          .AnyAsync(fun u -> u.Id = message.From.Id)

      let funcToExecute =
        if userExists then
          sendMessageAsync
        else
          addUserAndSendMessageAsync

      return! funcToExecute message data
    }

  let handleEmptyCommandAsync message =
    _unauthorizedUserCommandHandler.HandleAsync message

  member this.HandleAsync(message: Message) =
    match message.Text with
    | CommandData data -> handleCommandDataAsync message data
    | _ -> handleEmptyCommandAsync message
