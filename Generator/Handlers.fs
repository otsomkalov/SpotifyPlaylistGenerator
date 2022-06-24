module Generator.Handlers

open System
open System.Threading.Tasks
open Amazon.SQS
open Database
open Generator.Bot.Env
open Generator.Bot.Services
open Generator.ExternalResponses.Spotify
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Shared.Services
open Shared.Settings
open SpotifyAPI.Web
open Telegram.Bot
open Telegram.Bot.Types
open Giraffe
open Telegram.Bot.Types.Enums

module Telegram =
  let updateHandler (update: Update) : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
      task {
        let appEnv =
          BotEnv(
            ctx.GetService<ITelegramBotClient>(),
            ctx.GetService<AppDbContext>(),
            ctx.GetService<SpotifyClientProvider>(),
            ctx.GetService<IAmazonSQS>(),
            ctx.GetService<IOptions<AmazonSettings>>(),
            ctx.GetService<IOptions<SpotifySettings>>()
          )

        let loggerFactory =
          ctx.GetService<ILoggerFactory>()

        let logger =
          loggerFactory.CreateLogger("Logger")

        let handleUpdateFunc : Task<unit> =
          match update.Type with
          | UpdateType.CallbackQuery -> CallbackQueryService.handle update.CallbackQuery appEnv
          | UpdateType.Message -> MessageService.handle update.Message appEnv
          | _ -> Task.FromResult()

        try
          do! handleUpdateFunc
        with
        | e -> logger.LogError(e, "Error during processing update:")

        return! Successful.NO_CONTENT next ctx
      }

module Spotify =
  let callbackHandler (response: CodeResponse) : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
      task {
        let _spotifySettings =
          ctx.GetService<IOptions<SpotifySettings>>().Value

        let _spotifyClientProvider =
          ctx.GetService<SpotifyClientProvider>()

        let _telegramSettings =
          ctx.GetService<IOptions<TelegramSettings>>().Value

        let! tokenResponse =
          (_spotifySettings.ClientId, _spotifySettings.ClientSecret, response.Code, _spotifySettings.CallbackUrl)
          |> AuthorizationCodeTokenRequest
          |> OAuthClient().RequestToken

        let spotifyClient =
          (_spotifySettings.ClientId, _spotifySettings.ClientSecret, tokenResponse)
          |> AuthorizationCodeAuthenticator
          |> SpotifyClientConfig
            .CreateDefault()
            .WithAuthenticator
          |> SpotifyClient

        let! spotifyUserProfile = spotifyClient.UserProfile.Current()

        (spotifyUserProfile.Id, spotifyClient)
        |> _spotifyClientProvider.SetClient

        return! redirectTo true $"{_telegramSettings.BotUrl}?start={spotifyUserProfile.Id}" next ctx
      }

module Shared =
  let errorHandler (ex: Exception) (logger: ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")

    clearResponse
    >=> setStatusCode 500
    >=> text ex.Message

  let parsingErrorHandler (err: string) = RequestErrors.BAD_REQUEST err

  let notFoundHandler: HttpHandler =
    setStatusCode 404 >=> text "Not Found"
