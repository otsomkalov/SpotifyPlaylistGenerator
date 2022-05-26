namespace Generator

#nowarn "20"

open Generator.Bot.Services
open Generator.Bot.Services.Playlist
open Generator.Worker.Services
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Shared

module Program =

  let configureServices (services: IServiceCollection) (configuration: IConfiguration) =
    services
    |> Startup.addSettings configuration
    |> Startup.addServices

    services.AddHostedService<Worker>()

    services.AddApplicationInsightsTelemetry()

    services.AddLocalization()

    services.AddControllers().AddNewtonsoftJson()

  [<EntryPoint>]
  let main args =

    let builder =
      WebApplication.CreateBuilder(args)

    configureServices builder.Services builder.Configuration

    let app = builder.Build()

    app.MapControllers()

    app.Run()

    0
