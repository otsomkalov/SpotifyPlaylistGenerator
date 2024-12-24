module Infrastructure.Startup

#nowarn "20"

open Azure.Storage.Queues
open Domain.Core
open Domain.Repos
open Infrastructure.Repos
open Infrastructure.Settings
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Options
open MongoDB.ApplicationInsights
open MongoDB.Driver
open StackExchange.Redis
open MongoDB.ApplicationInsights.DependencyInjection
open otsom.fs.Telegram.Bot.Auth.Spotify.Settings
open otsom.fs.Extensions.DependencyInjection

let private configureRedisCache (options: IOptions<RedisSettings>) =
  let settings = options.Value

  ConnectionMultiplexer.Connect(settings.ConnectionString) :> IConnectionMultiplexer

let private configureQueueClient (options: IOptions<StorageSettings>) =
  let settings = options.Value

  QueueClient(settings.ConnectionString, settings.QueueName)

let private configureMongoClient (factory: IMongoClientFactory) (options: IOptions<DatabaseSettings>) =
  let settings = options.Value

  factory.GetClient(settings.ConnectionString)

let private configureMongoDatabase (options: IOptions<DatabaseSettings>) (mongoClient: IMongoClient) =
  let settings = options.Value

  mongoClient.GetDatabase(settings.Name)

let addInfrastructure (configuration: IConfiguration) (services: IServiceCollection) =
  services.Configure<DatabaseSettings>(configuration.GetSection(DatabaseSettings.SectionName))
  services.Configure<StorageSettings>(configuration.GetSection(StorageSettings.SectionName))
  services.Configure<RedisSettings>(configuration.GetSection(RedisSettings.SectionName))

  services.BuildSingleton<IConnectionMultiplexer, IOptions<RedisSettings>>(configureRedisCache)

  services.BuildSingleton<QueueClient, IOptions<StorageSettings>>(configureQueueClient)

  services.AddMongoClientFactory()
  services.BuildSingleton<IMongoClient, IMongoClientFactory, IOptions<DatabaseSettings>>(configureMongoClient)
  services.BuildSingleton<IMongoDatabase, IOptions<DatabaseSettings>, IMongoClient>(configureMongoDatabase)

  services.AddSingleton<IPresetRepo, PresetRepo>()
  services.AddSingleton<IUserRepo, UserRepo>()
