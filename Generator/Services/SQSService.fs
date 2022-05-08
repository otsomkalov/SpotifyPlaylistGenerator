namespace Generator.Services

open System.Collections.Generic
open System.Threading.Tasks
open Amazon.SQS
open Amazon.SQS.Model
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Shared.Settings

type SQSService(_sqs: IAmazonSQS, _amazonOptions: IOptions<AmazonSettings>, _logger: ILogger<SQSService>) =
  let _amazonSettings = _amazonOptions.Value

  let removeMessagesAsync (messages: Message seq) =
    task {
      let requests =
        messages
        |> Seq.map (fun m -> DeleteMessageBatchRequestEntry(m.MessageId, m.ReceiptHandle))
        |> List<DeleteMessageBatchRequestEntry>

      let! _ =
        DeleteMessageBatchRequest(_amazonSettings.QueueUrl, requests)
        |> _sqs.DeleteMessageBatchAsync

      return ()
    }

  let rec cleanupQueueAsync () =
    task {
      let! receiveMessagesResponse =
        ReceiveMessageRequest(_amazonSettings.QueueUrl, MaxNumberOfMessages = 10)
        |> _sqs.ReceiveMessageAsync

      return!
        if receiveMessagesResponse.Messages |> Seq.isEmpty then
          () |> Task.FromResult
        else
          task {
            do! removeMessagesAsync receiveMessagesResponse.Messages

            return! cleanupQueueAsync ()
          }
    }

  member this.CleanupQueueAsync() =
    task {
      _logger.LogInformation("Cleaning up queue...")
      do! cleanupQueueAsync ()
      _logger.LogInformation("Queue cleaned up.")

      return ()
    }
