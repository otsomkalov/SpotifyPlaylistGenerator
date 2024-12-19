module Telegram.Startup

open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Telegram.Core
open Telegram.Workflows
open otsom.fs.Extensions.DependencyInjection
open Domain.Core
open otsom.fs.Telegram.Bot.Core

let addBot (cfg: IConfiguration) (services: IServiceCollection) =
  services.BuildSingleton<User.SendCurrentPreset, User.Get, Preset.Get, SendUserKeyboard>(User.sendCurrentPreset)

  services
    .BuildSingleton<MessageHandlerMatcher, _>(faqMessageHandlerMatcher)
    .BuildSingleton<MessageHandlerMatcher, _>(privacyMessageHandlerMatcher)
