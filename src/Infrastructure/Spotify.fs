module Infrastructure.Spotify

open System
open System.Collections.Generic
open System.Threading.Tasks
open Domain.Repos
open Microsoft.Extensions.Options
open SpotifyAPI.Web
open otsom.fs.Core
open otsom.fs.Extensions
open otsom.fs.Telegram.Bot.Auth.Spotify.Settings
open otsom.fs.Telegram.Bot.Auth.Spotify.Workflows
open Domain.Integrations.Spotify.Helpers
open Domain.Integrations.Spotify

let rec private listLikedTracks' (client: ISpotifyClient) (offset: int) =
  async {
    let! tracks = client.Library.GetTracks(LibraryTracksRequest(Offset = offset, Limit = 50)) |> Async.AwaitTask

    return (tracks.Items |> Seq.map _.Track |> getTracksIds, tracks.Total)
  }

let listSpotifyLikedTracks (client: ISpotifyClient) : UserRepo.ListLikedTracks =
    let likedTacksLimit = 50

    let listLikedTracks' = listLikedTracks' client
    let loadTracks' = PlaylistRepo.loadTracks' likedTacksLimit

    fun () -> loadTracks' listLikedTracks' |> Async.StartAsTask

type GetClient = UserId -> Task<ISpotifyClient option>

let getClient (loadCompletedAuth: Completed.Load) (spotifyOptions: IOptions<SpotifySettings>) : GetClient =
  let spotifySettings = spotifyOptions.Value
  let clients = Dictionary<UserId, ISpotifyClient>()

  fun userId ->
    match clients.TryGetValue(userId) with
    | true, client -> client |> Some |> Task.FromResult
    | false, _ ->
      userId
      |> loadCompletedAuth
      |> TaskOption.taskMap (fun auth ->
        task {
          let! tokenResponse =
            AuthorizationCodeRefreshRequest(spotifySettings.ClientId, spotifySettings.ClientSecret, auth.Token)
            |> OAuthClient().RequestToken

          let retryHandler =
            SimpleRetryHandler(RetryAfter = TimeSpan.FromSeconds(30), RetryTimes = 3, TooManyRequestsConsumesARetry = true)

          let config =
            SpotifyClientConfig
              .CreateDefault()
              .WithRetryHandler(retryHandler)
              .WithToken(tokenResponse.AccessToken)

          return config |> SpotifyClient :> ISpotifyClient
        })
      |> TaskOption.tap (fun client -> clients.TryAdd(userId, client) |> ignore)