namespace Generator.Bot.Services

open System.Threading.Tasks
open Generator.Bot
open Database
open Database.Entities
open Domain.Core
open Domain.Workflows
open Infrastructure
open Infrastructure.Spotify
open Infrastructure.Workflows
open Shared.Services
open SpotifyAPI.Web
open Telegram.Bot
open Telegram.Bot.Types
open Generator.Bot.Helpers
open Microsoft.EntityFrameworkCore

type StartCommand = { AuthState: Telegram.Core.AuthState; Text: string }
type ProcessStartCommand = StartCommand -> Task<unit>

type StartCommandHandler
  (
    _bot: ITelegramBotClient,
    _context: AppDbContext,
    _spotifyClientProvider: SpotifyClientProvider,
    _unauthorizedUserCommandHandler: UnauthorizedUserCommandHandler,
    getState: State.GetState,
    createClientFromTokenResponse: CreateClientFromTokenResponse,
    cacheToken: TokenProvider.CacheToken,
    checkAuth: Telegram.Core.CheckAuth
  ) =

  let sendMessageAsync sendKeyboard (message: Message) =

    let loadPreset = Preset.load _context
    let getCurrentPresetId = Infrastructure.Workflows.User.getCurrentPresetId _context
    let getPresetMessage = Telegram.Workflows.getPresetMessage loadPreset
    let sendCurrentPresetInfo = Telegram.Workflows.sendCurrentPresetInfo sendKeyboard getCurrentPresetId getPresetMessage

    sendCurrentPresetInfo (message.From.Id |> UserId)

  let handleCommandDataAsync sendKeyboard (message: Message) (stateKey: string) =
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

            return! sendMessageAsync sendKeyboard message
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

  let overwriteToken sendKeyboard (message: Message) stateKey =
    let userId = message.From.Id |> UserId

    task {
      let! state = getState (stateKey |> State.StateKey.parse)

      return!
        match state with
        | Some state ->
          task{
            do! cacheToken userId state

            let spotifyClient =
              AuthorizationCodeTokenResponse(RefreshToken = state)
              |> createClientFromTokenResponse

            do _spotifyClientProvider.SetClient((userId |> UserId.value), spotifyClient)

            return! sendMessageAsync sendKeyboard message
          }
        | None ->
          _unauthorizedUserCommandHandler.HandleAsync message
    }

  member this.HandleAsync sendKeyboard (message: Message) =
    let userId = message.From.Id |> UserId

    task{
      let! authState = checkAuth userId

      return!
        match authState with
        | Telegram.Core.Authorized ->
          match message.Text with
          | CommandData stateKey -> overwriteToken sendKeyboard message stateKey
          | _ -> sendMessageAsync sendKeyboard message
        | Telegram.Core.Unauthorized ->
          match message.Text with
          | CommandData stateKey -> overwriteToken sendKeyboard message stateKey
          | _ -> handleEmptyCommandAsync message
    }