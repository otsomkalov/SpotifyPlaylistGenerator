[<RequireQualifiedAccess>]
module internal Infrastructure.Redis

open System
open Microsoft.ApplicationInsights
open Microsoft.ApplicationInsights.DataContracts
open StackExchange.Redis
open otsom.fs.Extensions

let prependList (telemetryClient: TelemetryClient) (cache: IDatabase) =
  fun (key: string) values ->
    task {
      let dependency = DependencyTelemetry("Redis", key, "prependList", key)

      use operation = telemetryClient.StartOperation dependency

      let! _ = cache.ListLeftPushAsync(key, values |> List.toArray)

      operation.Telemetry.Success <- true

      return ()
    }

let replaceList (telemetryClient: TelemetryClient) (cache: IDatabase) =
  fun (key: string) values ->
    task {
      let dependency = DependencyTelemetry("Redis", key, "replaceList", key)

      use operation = telemetryClient.StartOperation dependency

      let transaction = cache.CreateTransaction()

      let _ = transaction.KeyDeleteAsync(key)
      let _ = transaction.ListLeftPushAsync(key, values |> List.toArray)
      let _ = transaction.KeyExpireAsync(key, TimeSpan.FromDays(1))

      let! _ = transaction.ExecuteAsync() |> Task.map ignore

      operation.Telemetry.Success <- true

      return ()
    }

let loadList (telemetryClient: TelemetryClient) (cache: IDatabase) =
  fun key ->
    task {
      let dependency = DependencyTelemetry("Redis", key, "loadList", key)

      use operation = telemetryClient.StartOperation dependency

      let! values = key |> cache.ListRangeAsync

      operation.Telemetry.Success <- values.Length > 0

      return values
    }

let listLength (telemetryClient: TelemetryClient) (cache: IDatabase) =
  fun key ->
    task {
      let dependency = DependencyTelemetry("Redis", key, "listLength", key)

      use operation = telemetryClient.StartOperation dependency

      let! value = key |> cache.ListLengthAsync

      operation.Telemetry.Success <- true

      return value
    }
