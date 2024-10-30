namespace Generator.Functions

open System.Threading.Tasks
open Infrastructure.Telegram.Services
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.Functions.Worker
open Microsoft.Azure.Functions.Worker.Http
open Microsoft.Extensions.Logging
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums

type UpdateFunctions(_messageService: MessageService, _logger: ILogger<UpdateFunctions>, _callbackQueryService: CallbackQueryService) =
  inherit ControllerBase()

  [<Function("HandleUpdateAsync")>]
  member this.HandleUpdateAsync([<HttpTrigger(AuthorizationLevel.Function, "POST", Route = "telegram/update")>] request: HttpRequest, [<FromBody>]update: Update) =
    task {
      try
        let handleUpdateTask =
          match update.Type with
          | UpdateType.Message when update.Message.Type = MessageType.Text -> _messageService.ProcessAsync update.Message
          | UpdateType.CallbackQuery -> _callbackQueryService.ProcessAsync update.CallbackQuery
          | _ -> () |> Task.FromResult

        do! handleUpdateTask
      with
      | e -> _logger.LogError(e, "Error during processing update:")
    }
