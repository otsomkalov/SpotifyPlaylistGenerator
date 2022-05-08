module Shared.Startup

#nowarn "20"

open System
open Amazon
open Amazon.Runtime
open Amazon.SQS
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Options
open Shared.Data
open Shared.Services
open Shared.Settings
open Telegram.Bot

let private configureTelegramBotClient (serviceProvider: IServiceProvider) =
  let settings =
    serviceProvider
      .GetRequiredService<IOptions<TelegramSettings>>()
      .Value

  settings.Token |> TelegramBotClient :> ITelegramBotClient

let private configureSQS (_: IServiceProvider) =
  (EnvironmentVariablesAWSCredentials(), RegionEndpoint.EUCentral1)
  |> AmazonSQSClient
  :> IAmazonSQS

let private configureDbContext (serviceProvider: IServiceProvider) (builder: DbContextOptionsBuilder) =
  let settings =
    serviceProvider
      .GetRequiredService<IOptions<DatabaseSettings>>()
      .Value

  builder.UseNpgsql(settings.ConnectionString)

  ()

let addSettings (configuration: IConfiguration) (services: IServiceCollection) =
  services.Configure<SpotifySettings>(configuration.GetSection(SpotifySettings.SectionName))
  services.Configure<TelegramSettings>(configuration.GetSection(TelegramSettings.SectionName))
  services.Configure<DatabaseSettings>(configuration.GetSection(DatabaseSettings.SectionName))
  services.Configure<AmazonSettings>(configuration.GetSection(AmazonSettings.SectionName))

let addServices (services: IServiceCollection) =
  services.AddSingleton<SpotifyClientProvider>()

  services.AddSingleton<IAmazonSQS>(configureSQS)
  services.AddSingleton<ITelegramBotClient>(configureTelegramBotClient)

  services.AddApplicationInsightsTelemetry()

let addDbContext (lifetime: ServiceLifetime) (services: IServiceCollection) =
  services.AddDbContext<AppDbContext>(configureDbContext, lifetime)
