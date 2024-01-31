module Infrastructure.Telegram.Startup

#nowarn "20"

open Generator.Settings
open Infrastructure.Telegram.Services
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Options
open Telegram.Bot
open otsom.FSharp.Extensions.ServiceCollection

let private configureTelegramBotClient (options: IOptions<TelegramSettings>) =
  let settings = options.Value

  settings.Token |> TelegramBotClient :> ITelegramBotClient

let addTelegram (configuration: IConfiguration) (services: IServiceCollection) =
  services.Configure<TelegramSettings>(configuration.GetSection(TelegramSettings.SectionName))

  services.AddSingletonFunc<ITelegramBotClient, IOptions<TelegramSettings>>(configureTelegramBotClient)

  services.AddScoped<MessageService>().AddScoped<CallbackQueryService>()

  services.AddSingleton<SpotifyClientProvider>()
