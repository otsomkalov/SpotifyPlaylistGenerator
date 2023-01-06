namespace Generator

#nowarn "20"

open System
open Azure.Storage.Queues
open Generator.Bot.Services
open Generator.Bot.Services.Playlist
open Generator.Worker.Services
open Microsoft.Azure.Functions.Extensions.DependencyInjection
open Microsoft.Extensions.Caching.StackExchangeRedis
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Options
open Shared
open Shared.Settings

type Startup() =
  inherit FunctionsStartup()

  let configureRedisCache (configuration: IConfiguration) (options: RedisCacheOptions) =
    options.Configuration <- configuration[ConnectionStrings.Redis]

  let configureQueueClient (sp: IServiceProvider) =
    let settings = sp.GetRequiredService<IOptions<StorageSettings>>().Value

    QueueClient(settings.ConnectionString, settings.QueueName(*, QueueClientOptions(MessageEncoding = QueueMessageEncoding.Base64)*))

  override this.ConfigureAppConfiguration(builder: IFunctionsConfigurationBuilder) =

    builder.ConfigurationBuilder.AddUserSecrets<Startup>(true)

    ()

  override this.Configure(builder: IFunctionsHostBuilder) : unit =
    let configuration =
      builder.GetContext().Configuration
    let services = builder.Services

    services
    |> Startup.addSettings configuration
    |> Startup.addServices

    services.AddStackExchangeRedisCache(configureRedisCache configuration)

    services.AddSingleton<QueueClient>(configureQueueClient)

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