namespace Generator.Services

open System.Text.Json
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Shared.QueueMessages
open Shared.Services
open Shared.Settings
open SpotifyAPI.Web

type SpotifyLoginService
  (
    _spotifyOptions: IOptions<SpotifySettings>,
    _spotifyClientProvider: SpotifyClientProvider,
    _logger: ILogger<SpotifyLoginService>
  ) =
  let _spotifySettings = _spotifyOptions.Value

  member this.SaveLoginAsync(messageBody: string) =
    task {
      let loginMessage =
        JsonSerializer.Deserialize<SpotifyLoginMessage>(messageBody)

      _logger.LogInformation("Received login message for Spotify user with id {SpotifyId}", loginMessage.SpotifyId)

      let spotifyClient =
        (_spotifySettings.ClientId, _spotifySettings.ClientSecret, loginMessage.TokenResponse)
        |> AuthorizationCodeAuthenticator
        |> SpotifyClientConfig
          .CreateDefault()
          .WithAuthenticator
        |> SpotifyClient

      (loginMessage.SpotifyId, spotifyClient)
      |> _spotifyClientProvider.SetClient
    }
