namespace Generator.Bot.Services

open Resources
open Database
open Database.Entities
open Domain.Core
open Domain.Workflows
open Infrastructure
open Infrastructure.Spotify
open Microsoft.Extensions.Logging
open Shared.Services
open SpotifyAPI.Web
open Telegram.Bot
open Telegram.Bot.Types
open Generator.Bot.Helpers
open Microsoft.EntityFrameworkCore
open Telegram.Bot.Types.ReplyMarkups

type StartCommandHandler
  (
    _bot: ITelegramBotClient,
    _context: AppDbContext,
    _spotifyClientProvider: SpotifyClientProvider,
    _unauthorizedUserCommandHandler: UnauthorizedUserCommandHandler,
    _logger: ILogger<StartCommandHandler>,
    getState: State.GetState,
    createClientFromTokenResponse: CreateClientFromTokenResponse,
    cacheToken: TokenProvider.CacheToken
  ) =

  let sendMessageAsync (message: Message) =
    task {
      let replyMarkup =
        ReplyKeyboardMarkup(
          seq {
            seq { KeyboardButton(Messages.MyPresets) }
            seq { KeyboardButton(Messages.IncludePlaylist) }
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

  let handleCommandDataAsync (message: Message) (stateKey: string) =
    let userId = message.From.Id |> UserId

    task{
      let! state = getState (stateKey |> State.StateKey.parse)

      return!
        match state with
        | Some s ->
          task{
            do! cacheToken userId s

            let spotifyClient =
              AuthorizationCodeTokenResponse(RefreshToken = s)
              |> createClientFromTokenResponse

            do _spotifyClientProvider.SetClient((userId |> UserId.value), spotifyClient)

            return! sendMessageAsync message
          }
        | None ->
          _unauthorizedUserCommandHandler.HandleAsync message
    }

  let createUserAsync userId =
    task {
      let user = Database.Entities.User(Id = userId)
      let preset = Preset(Name = "Default", User = user)

      let _ = preset |> _context.Presets.AddAsync

      let! _ = _context.SaveChangesAsync()

      user.CurrentPreset <- preset

      let _ = _context.Users.Update(user)

      let! _ = _context.SaveChangesAsync()

      ()
    }

  let handleEmptyCommandAsync (message: Message) =
    task {
      let! userExists = _context.Users.AsNoTracking().AnyAsync(fun u -> u.Id = message.From.Id)

      if not userExists then
        do! createUserAsync message.From.Id

      return! _unauthorizedUserCommandHandler.HandleAsync message
    }

  member this.HandleAsync(message: Message) =
    match message.Text with
    | CommandData stateKey -> handleCommandDataAsync message stateKey
    | _ -> handleEmptyCommandAsync message
