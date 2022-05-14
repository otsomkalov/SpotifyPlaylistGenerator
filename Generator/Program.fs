namespace Generator

#nowarn "20"

open Generator.Bot.Services
open Generator.Bot.Services.Playlist
open Generator.Worker.Services
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Shared

module Program =

  let configureServices (services: IServiceCollection) (configuration: IConfiguration) =
    services
    |> Startup.addSettings configuration
    |> Startup.addServices

    services
      .AddScoped<UnauthorizedUserCommandHandler>()
      .AddScoped<StartCommandHandler>()
      .AddScoped<GenerateCommandHandler>()
      .AddScoped<UnknownCommandHandler>()
      .AddScoped<EmptyCommandDataHandler>()

      .AddScoped<PlaylistCommandHandler>()
      .AddScoped<AddSourcePlaylistCommandHandler>()
      .AddScoped<SetTargetPlaylistCommandHandler>()
      .AddScoped<SetHistoryPlaylistCommandHandler>()
      .AddScoped<AddHistoryPlaylistCommandHandler>()

      .AddScoped<MessageService>()

    services
      .AddScoped<FileService>()
      .AddScoped<TracksIdsService>()
      .AddScoped<PlaylistService>()
      .AddScoped<HistoryPlaylistsService>()
      .AddScoped<LikedTracksService>()
      .AddScoped<PlaylistsService>()
      .AddScoped<TargetPlaylistService>()
      .AddScoped<GeneratorService>()

    services.AddHostedService<Worker>()

    services
      .AddApplicationInsightsTelemetry()
      .AddApplicationInsightsTelemetryWorkerService()

    services.AddControllers().AddNewtonsoftJson()

  [<EntryPoint>]
  let main args =

    let builder =
      WebApplication.CreateBuilder(args)

    configureServices builder.Services builder.Configuration

    let app = builder.Build()

    app.MapControllers()

    app.Run()

    0
