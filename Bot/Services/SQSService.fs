namespace Bot.Services

open System.Collections.Generic
open System.Text.Json
open Amazon.SQS
open Amazon.SQS.Model
open Microsoft.Extensions.Options
open Shared.QueueMessages
open Shared.Settings

type SQSService(_sqs: IAmazonSQS, _amazonOptions: IOptions<AmazonSettings>) =
  let _amazonSettings = _amazonOptions.Value

  let createRequest messageType groupId messageBody =
    let sendMessageRequest =
      SendMessageRequest(_amazonSettings.QueueUrl, messageBody, MessageGroupId = groupId)

    sendMessageRequest.MessageAttributes <-
      [ (MessageAttributeNames.Type, MessageAttributeValue(DataType = "String", StringValue = messageType)) ]
      |> dict
      |> Dictionary<string, MessageAttributeValue>

    sendMessageRequest

  member this.SendMessageAsync (content: QueueMessage) messageType =
    task {
      content
      |> JsonSerializer.Serialize
      |> createRequest messageType content.SpotifyId
      |> _sqs.SendMessageAsync
      |> ignore

      return ()
    }
