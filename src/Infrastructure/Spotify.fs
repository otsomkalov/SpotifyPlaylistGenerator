module Infrastructure.Spotify

open System
open System.Threading.Tasks
open Domain.Core
open Domain.Workflows
open Microsoft.Extensions.Options
open Shared.Settings
open SpotifyAPI.Web
open StackExchange.Redis
open Domain.Extensions

let getRecommendations (client: ISpotifyClient) : Preset.GetRecommendations =
  fun count tracks ->
    task{
      let request = RecommendationsRequest()

      for track in tracks do request.SeedTracks.Add(track)

      request.Limit <- count

      let! recommendationsResponse = client.Browse.GetRecommendations(request)

      return recommendationsResponse.Tracks |> Seq.map (fun t -> t.Id) |> Seq.toList
    }

let getTracksIds (tracks: FullTrack seq) =
  tracks
  |> Seq.filter (fun t -> isNull t |> not)
  |> Seq.map (fun t -> t.Id)
  |> Seq.toList

type CreateClientFromTokenResponse = AuthorizationCodeTokenResponse -> ISpotifyClient

let createClientFromTokenResponse (spotifySettings: IOptions<SpotifySettings>) : CreateClientFromTokenResponse =
  fun response ->
    let spotifySettings = spotifySettings.Value

    let authenticator =
      AuthorizationCodeAuthenticator(spotifySettings.ClientId, spotifySettings.ClientSecret, response)

    let retryHandler =
      SimpleRetryHandler(RetryAfter = TimeSpan.FromSeconds(30), RetryTimes = 3, TooManyRequestsConsumesARetry = true)

    let config =
      SpotifyClientConfig
        .CreateDefault()
        .WithAuthenticator(authenticator)
        .WithRetryHandler(retryHandler)

    config |> SpotifyClient :> ISpotifyClient

module TokenProvider =
  type CacheToken = UserId -> string -> Task<unit>

  let cacheToken (connectionMultiplexer: IConnectionMultiplexer) : CacheToken =
    fun userId token ->
      let database = connectionMultiplexer.GetDatabase 1

      database.StringSetAsync((userId |> UserId.value |> string), token, (TimeSpan.FromDays 7))
      |> Task.map ignore
