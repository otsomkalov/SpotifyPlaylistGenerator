module Infrastructure.Telegram.Startup

#nowarn "20"

open System
open Generator.Settings
open Infrastructure.Telegram.Services
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Options
open Telegram.Bot

let private configureTelegramBotClient (serviceProvider: IServiceProvider) =
  let settings =
    serviceProvider
      .GetRequiredService<IOptions<TelegramSettings>>()
      .Value

  settings.Token |> TelegramBotClient :> ITelegramBotClient

let addTelegram (configuration: IConfiguration) (services: IServiceCollection) =
  services.Configure<TelegramSettings>(configuration.GetSection(TelegramSettings.SectionName))

  services.AddSingleton<ITelegramBotClient>(configureTelegramBotClient)

  services.AddScoped<MessageService>().AddScoped<CallbackQueryService>()

  services.AddSingleton<SpotifyClientProvider>()
