namespace Generator

#nowarn "20"

open System
open Azure.Storage.Queues
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
open MongoDB.Driver
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

  let configureMongoClient (options: IOptions<DatabaseSettings>) =
    let settings = options.Value

    MongoClient(settings.ConnectionString) :> IMongoClient

  let configureMongoDatabase (options: IOptions<DatabaseSettings>) (mongoClient: IMongoClient) =
    let settings = options.Value

    mongoClient.GetDatabase(settings.Name)

  override this.ConfigureAppConfiguration(builder: IFunctionsConfigurationBuilder) =

    builder.ConfigurationBuilder.AddUserSecrets<Startup>(true)

    ()

  override this.Configure(builder: IFunctionsHostBuilder) : unit =
    let configuration = builder.GetContext().Configuration
    let services = builder.Services

    services |> Startup.addSettings configuration |> Startup.addServices

    services.AddSingleton<IConnectionMultiplexer>(configureRedisCache)

    services.AddSingleton<QueueClient>(configureQueueClient)

    services.AddSingletonFunc<IMongoClient, IOptions<DatabaseSettings>>(configureMongoClient)
    services.AddSingletonFunc<IMongoDatabase, IOptions<DatabaseSettings>, IMongoClient>(configureMongoDatabase)

    services
      .AddScoped<GenerateCommandHandler>()

      .AddScoped<AddSourcePlaylistCommandHandler>()
      .AddScoped<SetTargetPlaylistCommandHandler>()
      .AddScoped<AddHistoryPlaylistCommandHandler>()

      .AddScoped<SetPlaylistSizeCommandHandler>()

      .AddScoped<MessageService>()
      .AddScoped<CallbackQueryService>()

    services.AddLocalization()

    services.AddScopedFunc<Preset.Load, IMongoDatabase>(Preset.load)
    services.AddScopedFunc<Preset.Update, IMongoDatabase>(Preset.update)
    services.AddScopedFunc<User.Load, IMongoDatabase>(User.load)
    services.AddScopedFunc<User.Exists, IMongoDatabase>(User.exists)

    services.AddScopedFunc<Telegram.Core.CheckAuth, SpotifyClientProvider>(Telegram.checkAuth)

    services.AddSingletonFunc<Spotify.CreateClientFromTokenResponse, IOptions<SpotifySettings>>(Spotify.createClientFromTokenResponse)

    ()

[<assembly: FunctionsStartup(typeof<Startup>)>]
do ()
