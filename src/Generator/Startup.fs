namespace Generator

#nowarn "20"

open System
open Azure.Storage.Queues
open Database
open Domain.Core
open Generator.Bot
open Generator.Bot.Services
open Generator.Bot.Services.Playlist
open Infrastructure
open Infrastructure.Workflows
open Microsoft.Azure.Functions.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Options
open Shared
open Shared.Services
open Shared.Settings
open Generator.Extensions.ServiceCollection
open Domain.Workflows
open StackExchange.Redis

type Startup() =
  inherit FunctionsStartup()

  let configureRedisCache (serviceProvider: IServiceProvider) =
    let settings = serviceProvider.GetRequiredService<IOptions<RedisSettings>>().Value

    ConnectionMultiplexer.Connect(settings.ConnectionString) :> IConnectionMultiplexer

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

    services.AddSingleton<IConnectionMultiplexer>(configureRedisCache)

    services.AddSingleton<QueueClient>(configureQueueClient)

    services
      .AddScoped<UnauthorizedUserCommandHandler>()
      .AddScoped<StartCommandHandler>()
      .AddScoped<GenerateCommandHandler>()

      .AddScoped<AddSourcePlaylistCommandHandler>()
      .AddScoped<SetTargetPlaylistCommandHandler>()
      .AddScoped<AddHistoryPlaylistCommandHandler>()

      .AddScoped<SetPlaylistSizeCommandHandler>()

      .AddScoped<MessageService>()
      .AddScoped<CallbackQueryService>()

    services.AddLocalization()

    services.AddSingletonFunc<User.LoadCurrentPreset, AppDbContext>(User.loadCurrentPreset)
    services.AddSingletonFunc<User.ListPresets, AppDbContext>(User.listPresets)
    services.AddSingletonFunc<User.LoadPreset, AppDbContext>(User.loadPreset)
    services.AddSingletonFunc<User.GetCurrentPresetId, AppDbContext>(User.getCurrentPresetId)

    services.AddSingletonFunc<Preset.Load, AppDbContext>(Preset.load)

    services.AddScopedFunc<PresetSettings.Load, AppDbContext>(PresetSettings.load)
    services.AddScopedFunc<PresetSettings.Update, AppDbContext>(PresetSettings.update)
    services.AddScopedFunc<PresetSettings.SetPlaylistSize, PresetSettings.Load, PresetSettings.Update>(PresetSettings.setPlaylistSize)

    services.AddScopedFunc<Telegram.Core.GetPresetMessage, Preset.Load>(Telegram.Workflows.getPresetMessage)
    services.AddScopedFunc<Telegram.Core.CheckAuth, SpotifyClientProvider>(Telegram.checkAuth)

    services.AddSingletonFunc<State.GetState, IConnectionMultiplexer>(State.getState)
    services.AddSingletonFunc<State.SetState, IConnectionMultiplexer>(State.setState)

    services.AddSingletonFunc<Spotify.CreateClientFromTokenResponse, IOptions<SpotifySettings>>(Spotify.createClientFromTokenResponse)

    services.AddSingletonFunc<Spotify.TokenProvider.CacheToken, IConnectionMultiplexer>(Spotify.TokenProvider.cacheToken)

    ()

[<assembly: FunctionsStartup(typeof<Startup>)>]
do ()
