namespace Generator.Services

open System.Text.Json
open Microsoft.Extensions.Options
open Shared.QueueMessages
open Shared.Services
open Shared.Settings
open SpotifyAPI.Web

type SpotifyLoginService(_spotifyOptions: IOptions<SpotifySettings>, _spotifyClientProvider: SpotifyClientProvider) =
  let _spotifySettings = _spotifyOptions.Value

  member this.SaveLoginAsync(messageBody: string) =
    task {
      let loginMessage =
        JsonSerializer.Deserialize<SpotifyLoginMessage>(messageBody)

      let loginMessage' = loginMessage :> IMessage

      let spotifyClient =
        (_spotifySettings.ClientId, _spotifySettings.ClientSecret, loginMessage.TokenResponse)
        |> AuthorizationCodeAuthenticator
        |> SpotifyClientConfig
          .CreateDefault()
          .WithAuthenticator
        |> SpotifyClient

      (loginMessage'.SpotifyId, spotifyClient)
      |> _spotifyClientProvider.SetClient
    }
