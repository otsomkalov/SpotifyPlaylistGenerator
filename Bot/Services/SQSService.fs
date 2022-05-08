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

  let createRequest messageBody messageType groupId =
    let sendMessageRequest =
      SendMessageRequest(_amazonSettings.QueueUrl, messageBody, MessageGroupId = groupId)

    sendMessageRequest.MessageAttributes <-
      [ (MessageAttributeNames.Type, MessageAttributeValue(DataType = "String", StringValue = messageType)) ]
      |> dict
      |> Dictionary<string, MessageAttributeValue>

    sendMessageRequest

  member this.SendMessageAsync (content: IMessage) messageType =
    task {
      let messageBody =
        JsonSerializer.Serialize content

      let sendMessageRequest =
        createRequest messageBody messageType content.SpotifyId

      let! _ = _sqs.SendMessageAsync(sendMessageRequest)

      return ()
    }
