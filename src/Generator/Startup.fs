namespace Generator

#nowarn "20"

open Generator.Bot.Services
open Generator.Bot.Services.Playlist
open Generator.Worker.Services
open Microsoft.Azure.Functions.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Shared

type Startup() =
  inherit FunctionsStartup()

  override this.ConfigureAppConfiguration(builder: IFunctionsConfigurationBuilder) =

    builder.ConfigurationBuilder.AddUserSecrets<Startup>(true).AddEnvironmentVariables()

    ()

  override this.Configure(builder: IFunctionsHostBuilder) : unit =
    let configuration =
      builder.GetContext().Configuration
    let services = builder.Services

    services
    |> Startup.addSettings configuration
    |> Startup.addServices configuration

    services
      .AddScoped<UnauthorizedUserCommandHandler>()
      .AddScoped<StartCommandHandler>()
      .AddScoped<GenerateCommandHandler>()
      .AddScoped<UnknownCommandHandler>()
      .AddScoped<EmptyCommandDataHandler>()
      .AddScoped<SettingsCommandHandler>()

      .AddScoped<PlaylistCommandHandler>()
      .AddScoped<AddSourcePlaylistCommandHandler>()
      .AddScoped<SetTargetPlaylistCommandHandler>()
      .AddScoped<SetHistoryPlaylistCommandHandler>()
      .AddScoped<AddHistoryPlaylistCommandHandler>()

      .AddScoped<GetSettingsMessageCommandHandler>()
      .AddScoped<SetIncludeLikedTracksCommandHandler>()
      .AddScoped<SetPlaylistSizeCommandHandler>()

      .AddScoped<MessageService>()
      .AddScoped<CallbackQueryService>()

    services
      .AddScoped<FileService>()
      .AddScoped<TracksIdsService>()
      .AddScoped<PlaylistService>()
      .AddScoped<HistoryPlaylistsService>()
      .AddScoped<LikedTracksService>()
      .AddScoped<PlaylistsService>()
      .AddScoped<TargetPlaylistService>()
      .AddScoped<GeneratorService>()

    services.AddLocalization()

    ()

[<assembly: FunctionsStartup(typeof<Startup>)>]
do ()