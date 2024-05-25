module Infrastructure.Spotify

open System
open System.Net
open Domain.Core
open Domain.Workflows
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open SpotifyAPI.Web
open Infrastructure.Helpers
open otsom.fs.Extensions
open otsom.fs.Telegram.Bot.Auth.Spotify.Settings

[<Literal>]
let private recommendationsLimit = 100

[<Literal>]
let private playlistTracksLimit = 100

[<Literal>]
let private likedTacksLimit = 50

let getRecommendations logRecommendedTracks (client: ISpotifyClient) : Preset.GetRecommendations =
  fun tracks ->
    task {
      let request = RecommendationsRequest()

      for track in tracks |> List.takeSafe 5 do
        request.SeedTracks.Add(track |> TrackId.value)

      request.Limit <- recommendationsLimit

      let! recommendationsResponse = client.Browse.GetRecommendations(request)

      logRecommendedTracks recommendationsResponse.Tracks.Count

      return
        recommendationsResponse.Tracks
        |> Seq.map (fun st ->
          { Id = TrackId st.Id
            Artists = st.Artists |> Seq.map (fun a -> { Id = ArtistId a.Id }) |> Set.ofSeq })
        |> Seq.toList
    }

let private getTracksIds (tracks: FullTrack seq) =
  tracks
  |> Seq.filter (isNull >> not)
  |> Seq.filter (_.Id >> isNull >> not)
  |> Seq.map (fun st ->
    { Id = TrackId st.Id
      Artists = st.Artists |> Seq.map (fun a -> { Id = ArtistId a.Id }) |> Set.ofSeq })
  |> Seq.toList

[<Literal>]
let private mapParallelBatches = 15

let private loadTracks' limit loadBatch =
  async {
    let! initialBatch, totalCount = loadBatch 0

    return!
      match totalCount |> Option.ofNullable with
      | Some count ->
        [ limit..limit..count ]
        |> List.map (loadBatch >> Async.map fst)
        |> (fun batches -> (batches, mapParallelBatches))
        |> Async.Parallel
        |> Async.map (List.concat >> (List.append initialBatch))
      | None -> initialBatch |> async.Return
  }

[<RequireQualifiedAccess>]
module Playlist =
  let rec private listTracks' (client: ISpotifyClient) playlistId (offset: int) =
    async {
      let! tracks = client.Playlists.GetItems(playlistId, PlaylistGetItemsRequest(Offset = offset)) |> Async.AwaitTask

      return (tracks.Items |> Seq.map (fun x -> x.Track :?> FullTrack) |> getTracksIds, tracks.Total)
    }

  let listTracks (logger: ILogger) client : Playlist.ListTracks =
    fun playlistId ->
      let playlistId = playlistId |> ReadablePlaylistId.value |> PlaylistId.value
      let listPlaylistTracks = listTracks' client playlistId
      let loadTracks' = loadTracks' playlistTracksLimit

      task {
        try
          return! loadTracks' listPlaylistTracks

        with Spotify.ApiException e when e.Response.StatusCode = HttpStatusCode.NotFound ->
          logger.LogInformation("Playlist with id {PlaylistId} not found in Spotify", playlistId)

          return []
      }

[<RequireQualifiedAccess>]
module User =
  let rec private listLikedTracks' (client: ISpotifyClient) (offset: int) =
    async {
      let! tracks = client.Library.GetTracks(LibraryTracksRequest(Offset = offset, Limit = 50)) |> Async.AwaitTask

      return (tracks.Items |> Seq.map _.Track |> getTracksIds, tracks.Total)
    }

  let listLikedTracks (client: ISpotifyClient) : User.ListLikedTracks =
    let listLikedTracks' = listLikedTracks' client
    let loadTracks' = loadTracks' likedTacksLimit

    fun () -> loadTracks' listLikedTracks' |> Async.StartAsTask

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
