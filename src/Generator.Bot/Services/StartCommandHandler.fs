namespace Generator.Bot.Services

open System.Collections.Generic
open System.Threading.Tasks
open Generator.Bot
open Domain.Core
open Domain.Workflows
open Infrastructure
open Infrastructure.Workflows
open Infrastructure.Spotify
open Microsoft.Extensions.Options
open MongoDB.Driver
open Resources
open Shared.Services
open Shared.Settings
open SpotifyAPI.Web
open Telegram.Bot
open Telegram.Bot.Types
open Generator.Bot.Helpers
open Telegram.Bot.Types.ReplyMarkups
open Domain.Extensions

type StartCommand = { AuthState: Telegram.Core.AuthState; Text: string }
type ProcessStartCommand = StartCommand -> Task<unit>

type StartCommandHandler
  (
    _bot: ITelegramBotClient,
    _spotifyClientProvider: SpotifyClientProvider,
    _unauthorizedUserCommandHandler: UnauthorizedUserCommandHandler,
    getState: State.GetState,
    createClientFromTokenResponse: CreateClientFromTokenResponse,
    cacheToken: TokenProvider.CacheToken,
    checkAuth: Telegram.Core.CheckAuth,
    loadPreset: Preset.Load,
    loadUser: User.Load,
    userExists: User.Exists,
    _database: IMongoDatabase,
    _spotifyOptions: IOptions<SpotifySettings>
  ) =

  let _spotifySettings = _spotifyOptions.Value

  let sendMessageAsync sendKeyboard (message: Message) =

    let sendCurrentPresetInfo = Telegram.Workflows.sendCurrentPresetInfo loadUser loadPreset sendKeyboard

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

    let defaultPresetId = PresetId.create()

    let user ={
      Id = UserId userId
      CurrentPresetId = Some defaultPresetId
      Presets = [
        {Id = defaultPresetId
         Name = "Default"}
      ]
    }

    let createUser = User.create _database

    createUser user

  let handleEmptyCommandAsync (message: Message) =
    task {
      let! userExists = userExists (UserId message.From.Id)

      if not userExists then
        do! createUserAsync message.From.Id

      let scopes =
        [ Scopes.PlaylistModifyPrivate
          Scopes.PlaylistModifyPublic
          Scopes.UserLibraryRead ]
        |> List<string>

      let loginRequest =
        LoginRequest(_spotifySettings.CallbackUrl, _spotifySettings.ClientId, LoginRequest.ResponseType.Code, Scope = scopes)

      let replyMarkup =
        InlineKeyboardButton(Buttons.Login, Url = loginRequest.ToUri().ToString())
        |> InlineKeyboardMarkup

      return!
        _bot.SendTextMessageAsync(ChatId(message.Chat.Id), Messages.Welcome, replyMarkup = replyMarkup)
        |> Task.map ignore
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