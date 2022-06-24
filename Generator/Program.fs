open Generator
open Generator.ExternalResponses.Spotify
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Shared
open Telegram.Bot.Types

let webApp =
  choose [ GET
           >=> choose [ subRoute
                          "/callback"
                          (choose [ route "/spotify"
                                    >=> tryBindQuery<CodeResponse> Handlers.Shared.parsingErrorHandler None Handlers.Spotify.callbackHandler ]) ]
           POST
           >=> choose [ routeBind<Update> "/" Handlers.Telegram.updateHandler ]
           Handlers.Shared.notFoundHandler ]

let configureLogging (builder: ILoggingBuilder) =
  builder.AddConsole().AddDebug() |> ignore

let builder = WebApplication.CreateBuilder()

let services =
  builder.Services
  |> Startup.addSettings builder.Configuration
  |> Startup.addServices

services
  .AddHostedService<Worker>()
  .AddApplicationInsightsTelemetry()
  .AddLocalization()
  .AddCors()
  .AddGiraffe()
|> ignore

let app = builder.Build()

let env =
  app.Services.GetRequiredService<IWebHostEnvironment>()

(match env.IsDevelopment() with
 | true -> app.UseDeveloperExceptionPage()
 | false ->
   app
     .UseGiraffeErrorHandler(Handlers.Shared.errorHandler)
     .UseHttpsRedirection())
  .UseStaticFiles()
  .UseGiraffe(webApp)

app.Run()
