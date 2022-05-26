namespace Generator

open System.Threading.Tasks
open Amazon.SQS
open Database
open Generator.Bot.Env
open Generator.Bot.Services
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Shared.Services
open Shared.Settings
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums

[<ApiController>]
[<Route("update")>]
type UpdateController
  (
    _logger: ILogger<UpdateController>,
    _bot: ITelegramBotClient,
    _context: AppDbContext,
    _spotifyClientProvider: SpotifyClientProvider,
    _sqs: IAmazonSQS,
    _amazonOptions: IOptions<AmazonSettings>,
    _spotifyOptions: IOptions<SpotifySettings>
  ) =
  inherit ControllerBase()

  [<HttpPost>]
  member this.HandleUpdateAsync(update: Update) =
    task {
      let appEnv =
        BotEnv(_bot, _context, _spotifyClientProvider, _sqs, _amazonOptions, _spotifyOptions)

      let handleUpdateTask =
        match update.Type with
        | UpdateType.Message -> MessageService.handle update.Message
        | UpdateType.CallbackQuery -> CallbackQueryService.handle update.CallbackQuery
        | _ -> fun _ -> () |> Task.FromResult

      try
        do! handleUpdateTask appEnv
      with
      | e -> _logger.LogError(e, "Error during processing update:")
    }
