module Shared.SQS

open System.Text.Json
open Amazon.SQS
open Shared.Settings

type ISQS =
  abstract Settings: AmazonSettings
  abstract SQS: IAmazonSQS

let sendMessage (env: #ISQS) data =
  task {
    let message = JsonSerializer.Serialize data

    let! _ = env.SQS.SendMessageAsync(env.Settings.QueueUrl, message)

    return ()
  }
