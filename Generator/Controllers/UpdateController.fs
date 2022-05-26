namespace Generator

open System.Threading.Tasks
open Database
open Generator.Bot.Services
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open Shared.AppEnv
open Shared.Services
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums

[<ApiController>]
[<Route("update")>]
type UpdateController
  (
    _logger: ILogger<UpdateController>,
    _callbackQueryService: CallbackQueryService,
    _bot: ITelegramBotClient,
    _context: AppDbContext,
    _spotifyClientProvider: SpotifyClientProvider
  ) =
  inherit ControllerBase()

  [<HttpPost>]
  member this.HandleUpdateAsync(update: Update) =
    task {
      let appEnv =
        AppEnv(_logger, _bot, _context, _spotifyClientProvider)

      let handleUpdateTask =
        match update.Type with
        | UpdateType.Message -> MessageService.handle update.Message
        | UpdateType.CallbackQuery -> _callbackQueryService.HandleAsync update.CallbackQuery
        | _ -> () |> Task.FromResult

      try
        do! handleUpdateTask
      with
      | e -> _logger.LogError(e, "Error during processing update:")
    }
