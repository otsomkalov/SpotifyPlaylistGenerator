module Infrastructure.Telegram.Repos

open Azure.Storage.Queues
open Infrastructure.Helpers
open otsom.fs.Extensions

[<RequireQualifiedAccess>]
module PresetRepo =
  let queueGeneration (queueClient: QueueClient) =
    fun userId presetId ->
      {| UserId = userId
         PresetId = presetId |}
      |> JSON.serialize
      |> queueClient.SendMessageAsync
      |> Task.map ignore
