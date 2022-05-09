namespace Generator

#nowarn "20"

open Generator.Services
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Shared

module Program =
  let configureServices (context: HostBuilderContext) (services: IServiceCollection) =
    services
    |> Startup.addSettings context.Configuration
    |> Startup.addServices
    |> Startup.addDbContext ServiceLifetime.Singleton

    services
      .AddSingleton<FileService>()
      .AddSingleton<TracksIdsService>()
      .AddSingleton<PlaylistService>()
      .AddSingleton<HistoryPlaylistsService>()
      .AddSingleton<LikedTracksService>()
      .AddSingleton<PlaylistsService>()
      .AddSingleton<TargetPlaylistService>()
      .AddSingleton<GeneratorService>()
      .AddSingleton<SpotifyLoginService>()
      .AddSingleton<SQSService>()
      .AddSingleton<AccountsService>()

    services.AddApplicationInsightsTelemetryWorkerService()

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
