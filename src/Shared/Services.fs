namespace Shared.Services

open System.Collections.Generic
open System.Threading.Tasks
open Domain.Workflows
open Infrastructure
open SpotifyAPI.Web
open StackExchange.Redis
open Infrastructure.Spotify

type SpotifyClientProvider(connectionMultiplexer: IConnectionMultiplexer, createClientFromTokenResponse: CreateClientFromTokenResponse) =
  let _clientsByTelegramId =
    Dictionary<int64, ISpotifyClient>()

  member this.GetAsync userId : Task<ISpotifyClient> =
    let userId = userId |> UserId.value

    if _clientsByTelegramId.ContainsKey(userId) then
      _clientsByTelegramId[userId] |> Task.FromResult
    else
      let cache = connectionMultiplexer.GetDatabase Cache.tokensDatabase

      task {
        let! tokenValue = userId |> string |> cache.StringGetAsync

        return!
          match tokenValue.IsNullOrEmpty with
          | true -> Task.FromResult null
          | false ->
            let client =
              AuthorizationCodeTokenResponse(RefreshToken = tokenValue)
              |> createClientFromTokenResponse

            this.SetClient(userId, client)

            client |> Task.FromResult
      }

  member this.SetClient(telegramId: int64, client: ISpotifyClient) =
    if _clientsByTelegramId.ContainsKey(telegramId) then
      ()
    else
      (telegramId, client) |> _clientsByTelegramId.Add
