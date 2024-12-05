module Generator.Startup

#nowarn "20"

open System
open System.Text.Json
open System.Text.Json.Serialization
open System.Reflection
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.ApplicationInsights
open Microsoft.Azure.Functions.Worker
open otsom.fs.Telegram.Bot

let private configureServices (builderContext: HostBuilderContext) (services: IServiceCollection) : unit =

  services.AddApplicationInsightsTelemetryWorkerService()
  services.ConfigureFunctionsApplicationInsights()

  let configuration = builderContext.Configuration

  services
  |> Domain.Startup.addDomain
  |> MusicPlatform.Spotify.Startup.addSpotifyMusicPlatform configuration
  |> Auth.Spotify.Startup.addTelegramBotSpotifyAuthCore configuration
  |> Auth.Spotify.Mongo.Startup.addMongoSpotifyAuth
  |> Telegram.Startup.addBot configuration
  |> Infrastructure.Startup.addInfrastructure configuration
  |> Infrastructure.Telegram.Startup.addTelegram configuration

  services.AddLocalization()

  services.AddMvcCore().AddNewtonsoftJson()

  ()

let private configureAppConfiguration _ (configBuilder: IConfigurationBuilder) =

  configBuilder.AddUserSecrets(Assembly.GetExecutingAssembly())

  ()

let private configureWebApp (builder: IFunctionsWorkerApplicationBuilder) =
  builder.Services.Configure<JsonSerializerOptions>(fun opts ->
    JsonFSharpOptions.Default().AddToJsonSerializerOptions(opts))

  ()

let private configureLogging (builder: ILoggingBuilder) =
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
