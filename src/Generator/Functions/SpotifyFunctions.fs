namespace Generator.Functions

open System.Threading.Tasks
open Domain
open Domain.Extensions
open Infrastructure
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.Functions.Worker
open Microsoft.Extensions.Options
open Shared.Settings
open Generator.Extensions.IQueryCollection
open StackExchange.Redis

type SpotifyFunctions(_telegramOptions: IOptions<TelegramSettings>, _connectionMultiplexer: IConnectionMultiplexer) =

  let _telegramSettings = _telegramOptions.Value

  [<Function("HandleCallbackAsync")>]
  member this.HandleCallbackAsync([<HttpTrigger(AuthorizationLevel.Anonymous, "GET", Route = "spotify/callback")>] request: HttpRequest) =
    let onSuccess (auth: Domain.Core.Auth.Fulfilled) =
      RedirectResult($"{_telegramSettings.BotUrl}?start={(auth.State |> Domain.Core.Auth.State.value)}", true) :> IActionResult

    let onError error =
      match error with
      | Core.Auth.StateNotFound -> BadRequestObjectResult("State not found in the cache") :> IActionResult

    match request.Query["state"], request.Query["code"] with
    | QueryParam state, QueryParam code ->
      let tryGetAuth = Infrastructure.Workflows.Auth.tryGetInitedAuth _connectionMultiplexer

      let saveCompletedAuth =
        Infrastructure.Workflows.Auth.saveFulfilledAuth _connectionMultiplexer

      let addCode = Domain.Workflows.Auth.fulfill tryGetAuth saveCompletedAuth

      addCode (state |> Domain.Core.Auth.State.parse) code
      |> TaskResult.either onSuccess onError
    | QueryParam _, _ -> BadRequestObjectResult("Code is empty") :> IActionResult |> Task.FromResult
    | _, QueryParam _ -> BadRequestObjectResult("State is empty") :> IActionResult |> Task.FromResult
    | _, _ ->
      BadRequestObjectResult("State and code are empty") :> IActionResult
      |> Task.FromResult
