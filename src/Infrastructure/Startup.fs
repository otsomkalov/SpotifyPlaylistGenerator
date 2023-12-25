module Infrastructure.Startup

#nowarn "20"

open Domain.Workflows
open Infrastructure.Extensions.ServiceCollection
open System
open Azure.Storage.Queues
open Infrastructure.Settings
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Options
open MongoDB.ApplicationInsights
open MongoDB.Driver
open StackExchange.Redis
open MongoDB.ApplicationInsights.DependencyInjection
open Infrastructure.Workflows

let private configureRedisCache (serviceProvider: IServiceProvider) =
  let settings = serviceProvider.GetRequiredService<IOptions<RedisSettings>>().Value

  ConnectionMultiplexer.Connect(settings.ConnectionString) :> IConnectionMultiplexer

let private configureQueueClient (sp: IServiceProvider) =
  let settings = sp.GetRequiredService<IOptions<StorageSettings>>().Value

  QueueClient(settings.ConnectionString, settings.QueueName)

let private configureMongoClient (factory: IMongoClientFactory) (options: IOptions<DatabaseSettings>) =
  let settings = options.Value

  factory.GetClient(settings.ConnectionString)

let private configureMongoDatabase (options: IOptions<DatabaseSettings>) (mongoClient: IMongoClient) =
  let settings = options.Value

  mongoClient.GetDatabase(settings.Name)

let addInfrastructure (configuration: IConfiguration) (services: IServiceCollection) =
  services.Configure<SpotifySettings>(configuration.GetSection(SpotifySettings.SectionName))
  services.Configure<DatabaseSettings>(configuration.GetSection(DatabaseSettings.SectionName))
  services.Configure<StorageSettings>(configuration.GetSection(StorageSettings.SectionName))
  services.Configure<RedisSettings>(configuration.GetSection(RedisSettings.SectionName))

  services.AddSingleton<IConnectionMultiplexer>(configureRedisCache)

  services.AddSingleton<QueueClient>(configureQueueClient)

  services.AddMongoClientFactory()
  services.AddSingletonFunc<IMongoClient, IMongoClientFactory, IOptions<DatabaseSettings>>(configureMongoClient)
  services.AddSingletonFunc<IMongoDatabase, IOptions<DatabaseSettings>, IMongoClient>(configureMongoDatabase)

  services.AddScopedFunc<Preset.Load, IMongoDatabase>(Preset.load)
  services.AddScopedFunc<Preset.Update, IMongoDatabase>(Preset.update)
  services.AddScopedFunc<User.Load, IMongoDatabase>(User.load)
  services.AddScopedFunc<User.Exists, IMongoDatabase>(User.exists)

  services.AddSingletonFunc<Spotify.CreateClientFromTokenResponse, IOptions<SpotifySettings>>(Spotify.createClientFromTokenResponse)
