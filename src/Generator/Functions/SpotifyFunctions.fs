namespace Generator

open System
open Database
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.Extensions.Options
open Shared.Services
open Shared.Settings
open SpotifyAPI.Web

type SpotifyFunctions
  (
    _spotifyClientProvider: SpotifyClientProvider,
    _spotifyOptions: IOptions<SpotifySettings>,
    _telegramOptions: IOptions<TelegramSettings>,
    _context: AppDbContext
  ) =

  let _spotifySettings = _spotifyOptions.Value

  let _telegramSettings = _telegramOptions.Value

  [<FunctionName("HandleCallbackAsync")>]
  member this.HandleCallbackAsync([<HttpTrigger(AuthorizationLevel.Anonymous, "GET", Route = "spotify/callback")>] request: HttpRequest) =
    task {
      let code = request.Query["code"]

      let! tokenResponse =
        (_spotifySettings.ClientId, _spotifySettings.ClientSecret, code, _spotifySettings.CallbackUrl)
        |> AuthorizationCodeTokenRequest
        |> OAuthClient().RequestToken

      let authenticator =
        AuthorizationCodeAuthenticator(_spotifySettings.ClientId, _spotifySettings.ClientSecret, tokenResponse)

      let retryHandler =
        SimpleRetryHandler(RetryAfter = TimeSpan.FromSeconds(30), RetryTimes = 3, TooManyRequestsConsumesARetry = true)

      let config =
        SpotifyClientConfig
          .CreateDefault()
          .WithAuthenticator(authenticator)
          .WithRetryHandler(retryHandler)

      let spotifyClient = config |> SpotifyClient

      let! spotifyUserProfile = spotifyClient.UserProfile.Current()

      (spotifyUserProfile.Id, spotifyClient) |> _spotifyClientProvider.SetClient

      return RedirectResult($"{_telegramSettings.BotUrl}?start={spotifyUserProfile.Id}", true)
    }
