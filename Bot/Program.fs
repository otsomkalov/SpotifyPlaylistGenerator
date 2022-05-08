namespace Bot

#nowarn "20"

open Bot.Services
open Bot.Services.Bot
open Bot.Services.Bot.Playlist
open Bot.Services.Playlist
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
    |> Startup.addDbContext ServiceLifetime.Scoped

    services
      .AddSingleton<SpotifyIdProvider>()

      .AddScoped<SQSService>()

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
