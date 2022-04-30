namespace Generator

open System
open System.Collections.Generic
open System.Diagnostics
open Generator.Settings
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Generator.Services
open Microsoft.Extensions.Configuration
open SpotifyAPI.Web

module Program =
    let configureServices (configuration: IConfiguration) (services: IServiceCollection) =
        services.Configure<Settings>(configuration)
        services.Configure<SpotifySettings>(configuration.GetSection(SpotifySettings.SectionName))

        services
            .AddSingleton<SpotifyClientProvider>()
            .AddSingleton<FileService>()
            .AddSingleton<TracksIdsService>()
            .AddSingleton<PlaylistService>()
            .AddSingleton<PlaylistsService>()
            .AddSingleton<LikedTracksService>()
            .AddSingleton<HistoryPlaylistsService>()
            .AddSingleton<TargetPlaylistService>()

        services.AddControllers()

    let openSpotifyLoginPage (spotifySettings: SpotifySettings) =
        let loginRequest =
            (Uri(spotifySettings.CallbackUrl), spotifySettings.ClientId, LoginRequest.ResponseType.Code)
            |> LoginRequest

        loginRequest.Scope <-
            [ Scopes.PlaylistModifyPrivate
              Scopes.PlaylistModifyPublic
              Scopes.UserLibraryRead ]
            |> List<string>

        let cleanedUri =
            loginRequest.ToUri().ToString().Replace("&", "^&")

        ("cmd", $"/c start {cleanedUri}") |> Process.Start

        ()

    [<EntryPoint>]
    let main args =
        let builder =
            WebApplication.CreateBuilder(args)

        let services = builder.Services
        let configuration = builder.Configuration

        configureServices configuration services

        let app = builder.Build()

        app.MapControllers()

        let spotifySettings =
            configuration
                .GetSection(SpotifySettings.SectionName)
                .Get<SpotifySettings>()

        openSpotifyLoginPage spotifySettings

        app.Run()

        0 // exit code
