namespace Generator

open System
open System.Threading
open System.Threading.Tasks
open Amazon.SQS
open Amazon.SQS.Model
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Shared.Settings
open Telegram.Bot
open Generator.Worker.Services

type Worker
  (
    _logger: ILogger<Worker>,
    _sqs: IAmazonSQS,
    _bot: ITelegramBotClient,
    _amazonOptions: IOptions<AmazonSettings>,
    _serviceScopeFactory: IServiceScopeFactory
  ) =
  inherit BackgroundService()
  let _amazonSettings = _amazonOptions.Value

  let serviceScope =
    _serviceScopeFactory.CreateScope()

  let _generatorService =
    serviceScope.ServiceProvider.GetRequiredService<GeneratorService>()

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

      _logger.LogInformation("Received {MessagesCount} messages", response.Messages.Count)

      let message =
        response.Messages |> Seq.tryHead

      return!
        match message with
        | Some m -> processMessageAsync m
        | None -> () |> Task.FromResult
    }

  override _.ExecuteAsync(ct: CancellationToken) =
    task {
      while not ct.IsCancellationRequested do
        try
          do! runAsync ()
        with
        | e -> _logger.LogError(e, "Error during generator execution:")

        do! TimeSpan.FromSeconds(30) |> Task.Delay
    }
