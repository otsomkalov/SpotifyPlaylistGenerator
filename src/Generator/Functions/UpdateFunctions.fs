namespace Generator

open System.Threading.Tasks
open Generator.Bot.Services
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.Extensions.Logging
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums

type UpdateFunctions(_messageService: MessageService, _logger: ILogger<UpdateFunctions>, _callbackQueryService: CallbackQueryService) =
  inherit ControllerBase()

  [<FunctionName("HandleUpdateAsync")>]
  member this.HandleUpdateAsync([<HttpTrigger(AuthorizationLevel.Function, "POST", Route = "telegram/update")>]update: Update) =
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
