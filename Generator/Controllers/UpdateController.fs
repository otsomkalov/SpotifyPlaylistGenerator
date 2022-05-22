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
      let handleUpdateTask =
        match update.Type with
        | UpdateType.Message -> _messageService.ProcessMessageAsync update.Message
        | UpdateType.CallbackQuery -> _callbackQueryService.HandleAsync update.CallbackQuery
        | _ -> () |> Task.FromResult

      try
        do! handleUpdateTask
      with
      | e -> _logger.LogError(e, "Error during processing update:")
    }
