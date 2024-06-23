namespace Generator.Functions

open System.Threading.Tasks
open Generator.Settings
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.Functions.Worker
open Microsoft.Extensions.Options
open Generator.Extensions.IQueryCollection
open StackExchange.Redis
open otsom.fs.Extensions
open otsom.fs.Telegram.Bot.Auth.Spotify

type SpotifyFunctions
  (
    _telegramOptions: IOptions<TelegramSettings>,
    _connectionMultiplexer: IConnectionMultiplexer,
    fulfillAuth: Auth.Fulfill
  ) =

  let _telegramSettings = _telegramOptions.Value

  [<Function("HandleCallbackAsync")>]
  member this.HandleCallbackAsync([<HttpTrigger(AuthorizationLevel.Anonymous, "GET", Route = "spotify/callback")>] request: HttpRequest) =
    let onSuccess (state: string) =
      RedirectResult($"{_telegramSettings.BotUrl}?start={state}", true) :> IActionResult

    let onError error =
      match error with
      | Auth.StateNotFound -> BadRequestObjectResult("State not found in the cache") :> IActionResult

    match request.Query["state"], request.Query["code"] with
    | QueryParam state, QueryParam code -> fulfillAuth state code |> TaskResult.either onSuccess onError
    | QueryParam _, _ -> BadRequestObjectResult("Code is empty") :> IActionResult |> Task.FromResult
    | _, QueryParam _ -> BadRequestObjectResult("State is empty") :> IActionResult |> Task.FromResult
    | _, _ ->
      BadRequestObjectResult("State and code are empty") :> IActionResult
      |> Task.FromResult
