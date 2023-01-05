namespace Generator

open System.Threading.Tasks
open Amazon.SQS
open Amazon.SQS.Model
open Microsoft.Azure.WebJobs
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Shared.Settings
open Telegram.Bot
open Generator.Worker.Services

type GeneratorFunctions
  (
    _logger: ILogger<GeneratorFunctions>,
    _sqs: IAmazonSQS,
    _bot: ITelegramBotClient,
    _amazonOptions: IOptions<AmazonSettings>,
    _serviceScopeFactory: IServiceScopeFactory,
    _generatorService: GeneratorService
  ) =
  let _amazonSettings = _amazonOptions.Value

  let processMessageAsync (message: Message) =
    task {
      do! _generatorService.GeneratePlaylistAsync message.Body

      let! _ =
        (_amazonSettings.QueueUrl, message.ReceiptHandle)
        |> _sqs.DeleteMessageAsync

      return ()
    }

  let runAsync () =
    task {
      let! response =
        ReceiveMessageRequest(_amazonSettings.QueueUrl, WaitTimeSeconds = 20)
        |> _sqs.ReceiveMessageAsync

      let message =
        response.Messages |> Seq.tryHead

      return!
        match message with
        | Some m -> processMessageAsync m
        | None -> () |> Task.FromResult
    }

  [<FunctionName("GenerateAsync")>]
  member this.GenerateAsync([<TimerTrigger("%GeneratorSchedule%")>] timerInfo: TimerInfo) =
    task {
      try
        do! runAsync ()
      with
      | e -> _logger.LogError(e, "Error during generator execution:")

      ()
    }
