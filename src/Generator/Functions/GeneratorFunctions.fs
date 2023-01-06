namespace Generator

open Microsoft.Azure.WebJobs
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Shared.QueueMessages
open Telegram.Bot
open Generator.Worker.Services

type GeneratorFunctions
  (
    _logger: ILogger<GeneratorFunctions>,
    _bot: ITelegramBotClient,
    _serviceScopeFactory: IServiceScopeFactory,
    _generatorService: GeneratorService
  ) =

  [<FunctionName("GenerateAsync")>]
  member this.GenerateAsync([<QueueTrigger("%Storage:QueueName%")>] message: GeneratePlaylistMessage) =
    task {
      try
        do! _generatorService.GeneratePlaylistAsync message
      with
      | e -> _logger.LogError(e, "Error during generator execution:")

      ()
    }
