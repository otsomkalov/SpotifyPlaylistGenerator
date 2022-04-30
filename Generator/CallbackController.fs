namespace Generator

open System
open Generator.Services
open Generator.Settings
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open SpotifyAPI.Web

[<ApiController>]
[<Route("[controller]")>]
type CallbackController
    (
        _logger: ILogger<CallbackController>,
        _generatorService: GeneratorService,
        _spotifyOptions: IOptions<SpotifySettings>,
        _spotifyClientProvider: SpotifyClientProvider
    ) =
    inherit ControllerBase()
    let _settings = _spotifyOptions.Value

    [<HttpGet>]
    member _.GetAsync(code: string) =
        task {
            let! token =
                (_settings.ClientId, _settings.ClientSecret, code, Uri(_settings.CallbackUrl))
                |> AuthorizationCodeTokenRequest
                |> OAuthClient().RequestToken

            token
            |> SpotifyClient
            |> _spotifyClientProvider.setClient

            do! _generatorService.generatePlaylist ()
        }
