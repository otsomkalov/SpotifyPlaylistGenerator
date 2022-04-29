namespace Generator

open System
open Generator
open Generator.Services
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Options
open SpotifyAPI.Web

module Program =

    let private configureSpotifyClient (serviceProvider: IServiceProvider) =
        let settings =
            serviceProvider
                .GetRequiredService<IOptions<Settings>>()
                .Value

        SpotifyClient(settings.Token) :> ISpotifyClient

    let private configureServices (hostBuilderContext: HostBuilderContext) (services: IServiceCollection) =
        let configuration =
            hostBuilderContext.Configuration

        services.Configure<Settings>(configuration)

        services.AddSingleton<ISpotifyClient>(configureSpotifyClient)

        services
            .AddSingleton<FileService>()
            .AddSingleton<TracksIdsService>()
            .AddSingleton<PlaylistService>()
            .AddSingleton<LikedTracksService>()
            .AddSingleton<HistoryPlaylistsService>()
            .AddSingleton<TargetPlaylistService>()

        services.AddHostedService<Worker>()

        ()

    [<EntryPoint>]
    let main args =
        Host
            .CreateDefaultBuilder(args)
            .ConfigureServices(configureServices)
            .Build()
            .Run()

        0 // exit code
