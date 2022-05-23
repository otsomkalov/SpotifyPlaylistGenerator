namespace Generator

open System
open System.Threading
open System.Threading.Tasks
open Amazon.SQS
open Amazon.SQS.Model
open Database
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Shared.AppEnv
open Shared.Services
open Shared.Settings
open Telegram.Bot
open Generator.Worker.Services

type Worker
  (
    _logger: ILogger<Worker>,
    _sqs: IAmazonSQS,
    _bot: ITelegramBotClient,
    _amazonOptions: IOptions<AmazonSettings>,
    _serviceScopeFactory: IServiceScopeFactory,
    _spotifyClientProvider: SpotifyClientProvider
  ) =
  inherit BackgroundService()
  let _amazonSettings = _amazonOptions.Value

  let serviceScope =
    _serviceScopeFactory.CreateScope()

  let _context =
    serviceScope.ServiceProvider.GetRequiredService<AppDbContext>()

  let processMessageAsync (message: Message) =
    task {
      let env = AppEnv(_logger, _bot, _context, _spotifyClientProvider)

      do! GeneratorService.generatePlaylistAsync env message.Body

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

  override _.ExecuteAsync(ct: CancellationToken) =
    task {
      while not ct.IsCancellationRequested do
        try
          do! runAsync ()
        with
        | e -> _logger.LogError(e, "Error during generator execution:")

        do! TimeSpan.FromSeconds(30) |> Task.Delay
    }
