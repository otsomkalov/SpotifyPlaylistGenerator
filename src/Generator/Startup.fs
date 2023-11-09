module Generator.Startup

#nowarn "20"

open System
open System.Text.Json
open System.Text.Json.Serialization
open Generator.Extensions.ServiceCollection
open System.Reflection
open Azure.Storage.Queues
open Domain.Core
open Generator.Bot
open Generator.Bot.Services
open Infrastructure
open Infrastructure.Workflows
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.ApplicationInsights
open Microsoft.Extensions.Options
open MongoDB.ApplicationInsights
open MongoDB.Driver
open Shared
open Shared.Services
open Shared.Settings
open Domain.Workflows
open Microsoft.Azure.Functions.Worker
open StackExchange.Redis
open MongoDB.ApplicationInsights.DependencyInjection

let configureRedisCache (serviceProvider: IServiceProvider) =
  let settings = serviceProvider.GetRequiredService<IOptions<RedisSettings>>().Value

  ConnectionMultiplexer.Connect(settings.ConnectionString) :> IConnectionMultiplexer

let configureQueueClient (sp: IServiceProvider) =
  let settings = sp.GetRequiredService<IOptions<StorageSettings>>().Value

  QueueClient(settings.ConnectionString, settings.QueueName)

let configureMongoClient (factory: IMongoClientFactory) (options: IOptions<DatabaseSettings>) =
  let settings = options.Value

  factory.GetClient(settings.ConnectionString)

let configureMongoDatabase (options: IOptions<DatabaseSettings>) (mongoClient: IMongoClient) =
  let settings = options.Value

  mongoClient.GetDatabase(settings.Name)

let configureServices (builderContext: HostBuilderContext) (services: IServiceCollection) : unit =

  services.AddApplicationInsightsTelemetryWorkerService()
  services.ConfigureFunctionsApplicationInsights()

  let configuration = builderContext.Configuration

  services |> Startup.addSettings configuration |> Startup.addServices

  services.AddSingleton<IConnectionMultiplexer>(configureRedisCache)

  services.AddSingleton<QueueClient>(configureQueueClient)

  services.AddMongoClientFactory()
  services.AddSingletonFunc<IMongoClient, IMongoClientFactory, IOptions<DatabaseSettings>>(configureMongoClient)
  services.AddSingletonFunc<IMongoDatabase, IOptions<DatabaseSettings>, IMongoClient>(configureMongoDatabase)

  services.AddScoped<MessageService>().AddScoped<CallbackQueryService>()

  services.AddLocalization()

  services.AddScopedFunc<Preset.Load, IMongoDatabase>(Preset.load)
  services.AddScopedFunc<Preset.Update, IMongoDatabase>(Preset.update)
  services.AddScopedFunc<User.Load, IMongoDatabase>(User.load)
  services.AddScopedFunc<User.Exists, IMongoDatabase>(User.exists)

  services.AddScopedFunc<Telegram.Core.CheckAuth, SpotifyClientProvider>(Telegram.checkAuth)

  services.AddSingletonFunc<Spotify.CreateClientFromTokenResponse, IOptions<SpotifySettings>>(Spotify.createClientFromTokenResponse)

  services.AddMvcCore().AddNewtonsoftJson()

  ()

let configureAppConfiguration _ (configBuilder: IConfigurationBuilder) =

  configBuilder.AddUserSecrets(Assembly.GetExecutingAssembly())

  ()

let configureWebApp (builder: IFunctionsWorkerApplicationBuilder) =
  builder.Services.Configure<JsonSerializerOptions>(fun opts ->
    JsonFSharpOptions.Default().AddToJsonSerializerOptions(opts))

  ()

let configureLogging (builder: ILoggingBuilder) =
  builder.AddFilter<ApplicationInsightsLoggerProvider>(String.Empty, LogLevel.Information)

  ()

let host =
  HostBuilder()
    .ConfigureFunctionsWebApplication(configureWebApp)
    .ConfigureAppConfiguration(configureAppConfiguration)
    .ConfigureLogging(configureLogging)
    .ConfigureServices(configureServices)
    .Build()

host.Run()
