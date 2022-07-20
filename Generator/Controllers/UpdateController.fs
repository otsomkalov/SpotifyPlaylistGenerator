namespace Generator

open System.Threading.Tasks
open Generator.Bot.Services
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums

[<ApiController>]
[<Route("update")>]
type UpdateController(_messageService: MessageService, _logger: ILogger<UpdateController>, _callbackQueryService: CallbackQueryService) =
  inherit ControllerBase()

  [<HttpPost>]
  member this.HandleUpdateAsync(update: Update) =
    task {
      try
        let handleUpdateTask =
          match update.Type with
          | UpdateType.Message -> _messageService.ProcessAsync update.Message
          | UpdateType.CallbackQuery -> _callbackQueryService.ProcessAsync update.CallbackQuery
          | _ -> () |> Task.FromResult

        do! handleUpdateTask
      with
      | e -> _logger.LogError(e, "Error during processing update:")
    }
