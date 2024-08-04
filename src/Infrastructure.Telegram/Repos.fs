module Infrastructure.Telegram.Repos

open System.Diagnostics
open Azure.Storage.Queues
open Domain.Core
open Infrastructure.Helpers
open otsom.fs.Core
open otsom.fs.Extensions
open Infrastructure.Queue

[<RequireQualifiedAccess>]
module PresetRepo =
  type GeneratePresetRequest = { UserId: UserId; PresetId: PresetId }

  let queueGeneration (queueClient: QueueClient) =
    fun userId presetId ->
      let request: BaseMessage<GeneratePresetRequest> =
        { OperationId = Activity.Current.ParentId
          Data = { UserId = userId; PresetId = presetId } }

      request |> JSON.serialize |> queueClient.SendMessageAsync |> Task.map ignore
