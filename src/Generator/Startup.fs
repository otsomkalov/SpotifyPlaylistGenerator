namespace Generator

#nowarn "20"

open System
open Azure.Storage.Queues
open Database
open Domain.Core
open Generator.Bot.Services
open Generator.Bot.Services.Playlist
open Generator.Worker.Services
open Infrastructure.Workflows
open Microsoft.Azure.Functions.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Options
open Shared
open Shared.Settings
open Generator.Extensions.ServiceCollection
open Domain.Workflows
open StackExchange.Redis

type Startup() =
  inherit FunctionsStartup()

  let configureRedisCache (serviceProvider: IServiceProvider) =
    let settings = serviceProvider.GetRequiredService<IOptions<RedisSettings>>().Value

    let multiplexer = ConnectionMultiplexer.Connect(settings.ConnectionString)

    multiplexer.GetDatabase()

  let configureQueueClient (sp: IServiceProvider) =
    let settings = sp.GetRequiredService<IOptions<StorageSettings>>().Value

    QueueClient(settings.ConnectionString, settings.QueueName)

  override this.ConfigureAppConfiguration(builder: IFunctionsConfigurationBuilder) =

    builder.ConfigurationBuilder.AddUserSecrets<Startup>(true)

    ()

  override this.Configure(builder: IFunctionsHostBuilder) : unit =
    let configuration = builder.GetContext().Configuration
    let services = builder.Services

    services |> Startup.addSettings configuration |> Startup.addServices

    services.AddSingleton<IDatabase>(configureRedisCache)

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
      .AddScoped<GeneratorService>()

    services.AddLocalization()

    services.AddSingletonFunc<User.Load, AppDbContext>(User.load)

    services.AddSingletonFunc<ValidateUserPlaylists.Action, User.Load>(ValidateUserPlaylists.validateUserPlaylists)

    services.AddScopedFunc<UserSettings.Load, AppDbContext>(UserSettings.load)
    services.AddScopedFunc<UserSettings.Update, AppDbContext>(UserSettings.update)
    services.AddScopedFunc<UserSettings.SetPlaylistSize, UserSettings.Load, UserSettings.Update>(UserSettings.setPlaylistSize)

    services.AddScopedFunc<UserSettings.SetLikedTracksHandling, UserSettings.Load, UserSettings.Update>(UserSettings.setLikedTracksHandling)

    ()

[<assembly: FunctionsStartup(typeof<Startup>)>]
do ()
