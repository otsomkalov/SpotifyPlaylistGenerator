module Infrastructure.Spotify

open System
open System.Net
open System.Threading.Tasks
open Domain.Core
open Domain.Workflows
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Shared.Settings
open SpotifyAPI.Web
open Domain.Extensions
open Infrastructure.Helpers

let getRecommendations logRecommendedTracks (client: ISpotifyClient) : Preset.GetRecommendations =
  fun count tracks ->
    task {
      let request = RecommendationsRequest()

      for track in tracks do
        request.SeedTracks.Add(track |> TrackId.value)

      request.Limit <- count

      let! recommendationsResponse = client.Browse.GetRecommendations(request)

      logRecommendedTracks recommendationsResponse.Tracks.Count

      return recommendationsResponse.Tracks |> Seq.map ((fun t -> t.Id) >> TrackId) |> Seq.toList
    }

let private getTracksIds (tracks: FullTrack seq) =
  tracks
  |> Seq.filter (fun t -> isNull t |> not)
  |> Seq.map (fun t -> t.Id)
  |> Seq.toList

[<RequireQualifiedAccess>]
module Playlist =
  let rec private listTracks' (client: ISpotifyClient) playlistId (offset: int) =
    task {
      let! tracks = client.Playlists.GetItems(playlistId, PlaylistGetItemsRequest(Offset = offset))

      let! nextTracksIds =
        if isNull tracks.Next then
          [] |> Task.FromResult
        else
          listTracks' client playlistId (offset + 100)

      let currentTracksIds =
        tracks.Items |> Seq.map (fun x -> x.Track :?> FullTrack) |> getTracksIds

      return List.append nextTracksIds currentTracksIds
    }

  let listTracks (logger: ILogger) client : Playlist.ListTracks =
    fun playlistId ->
      task {
        let playlistId = playlistId |> ReadablePlaylistId.value |> PlaylistId.value

        try
          return! listTracks' client playlistId 0 |> Task.map (List.map TrackId)
        with Spotify.ApiException e when e.Response.StatusCode = HttpStatusCode.NotFound ->
          logger.LogInformation("Playlist with id {PlaylistId} not found in Spotify", playlistId)

          return []
      }

[<RequireQualifiedAccess>]
module User =
  let rec private listLikedTracks' (client: ISpotifyClient) (offset: int) =
    task {
      let! tracks =
        client.Library.GetTracks(LibraryTracksRequest(Offset = offset, Limit = 50))

      let! nextTracksIds =
        if isNull tracks.Next then
          [] |> Task.FromResult
        else
          listLikedTracks' client (offset + 50)

      let currentTracksIds =
        tracks.Items |> Seq.map (fun x -> x.Track) |> getTracksIds

      return List.append nextTracksIds currentTracksIds
    }

  let listLikedTracks (client: ISpotifyClient) : User.ListLikedTracks =
    fun () ->
      listLikedTracks' client 0 |> Task.map (List.map TrackId)

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