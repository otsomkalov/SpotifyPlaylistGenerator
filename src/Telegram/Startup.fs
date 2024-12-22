module Telegram.Startup

open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Telegram.Core
open Telegram.Workflows
open otsom.fs.Extensions.DependencyInjection

let addBot (cfg: IConfiguration) (services: IServiceCollection) =
  services
    .BuildSingleton<MessageHandlerMatcher, _>(faqMessageHandlerMatcher)
    .BuildSingleton<MessageHandlerMatcher, _>(privacyMessageHandlerMatcher)
    .BuildSingleton<MessageHandlerMatcher, _>(guideMessageHandlerMatcher)
    .BuildSingleton<MessageHandlerMatcher, _>(helpMessageHandlerMatcher)
    .BuildSingleton<MessageHandlerMatcher, _, _, _, _>(settingsMessageHandlerMatcher)
    .BuildSingleton<MessageHandlerMatcher, _, _, _, _>(backMessageHandlerMatcher)
