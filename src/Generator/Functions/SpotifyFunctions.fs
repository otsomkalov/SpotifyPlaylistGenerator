namespace Generator

open Infrastructure
open System
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.Extensions.Options
open Shared.Settings
open SpotifyAPI.Web

type SpotifyFunctions
  (
    _spotifyOptions: IOptions<SpotifySettings>,
    _telegramOptions: IOptions<TelegramSettings>,
    setState: State.SetState
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

      let state = State.StateKey.create

      do! setState state tokenResponse.RefreshToken

      return RedirectResult($"{_telegramSettings.BotUrl}?start={(state |> State.StateKey.value)}", true)
    }
