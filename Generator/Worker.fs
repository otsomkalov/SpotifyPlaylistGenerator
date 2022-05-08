namespace Generator

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Amazon.SQS
open Amazon.SQS.Model
open Generator.Services
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Shared.QueueMessages
open Shared.Settings
open Telegram.Bot

type Worker
  (
    _logger: ILogger<Worker>,
    _sqs: IAmazonSQS,
    _bot: ITelegramBotClient,
    _amazonOptions: IOptions<AmazonSettings>,
    _generatorService: GeneratorService,
    _loginService: SpotifyLoginService,
    _sqsService: SQSService,
    _accountsService: AccountsService
  ) =
  inherit BackgroundService()
  let _amazonSettings = _amazonOptions.Value

  let processMessageAsync (message: Message) =
    task {
      let processMessageBodyFunction =
        match message.MessageAttributes[MessageAttributeNames.Type]
          .StringValue
          with
        | MessageTypes.SpotifyLogin -> _loginService.SaveLoginAsync
        | MessageTypes.GeneratePlaylist -> _generatorService.GeneratePlaylistAsync
        | MessageTypes.LinkAccounts -> _accountsService.LinkAsync

      do! processMessageBodyFunction message.Body

      let! _ =
        (_amazonSettings.QueueUrl, message.ReceiptHandle)
        |> _sqs.DeleteMessageAsync

      return ()
    }

  let runAsync () =
    task {
      let receiveMessageRequest =
        ReceiveMessageRequest(_amazonSettings.QueueUrl)

      receiveMessageRequest.MessageAttributeNames <- [ MessageAttributeNames.Type ] |> List<string>

      let! response = _sqs.ReceiveMessageAsync(receiveMessageRequest)

      let message =
        response.Messages |> Seq.tryHead

      return!
        match message with
        | Some m -> processMessageAsync m
        | None -> () |> Task.FromResult
    }

  override _.ExecuteAsync(ct: CancellationToken) =
    task {
      do! _sqsService.CleanupQueueAsync()

      while not ct.IsCancellationRequested do
        try
          do! runAsync ()
        with
        | e -> _logger.LogError(e, "Error during generator execution:")

        do! TimeSpan.FromSeconds(30) |> Task.Delay
    }
