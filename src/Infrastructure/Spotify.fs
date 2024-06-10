module Infrastructure.Spotify

open System
open System.Net
open Domain.Core
open Domain.Repos
open Domain.Workflows
open FSharp
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open SpotifyAPI.Web
open Infrastructure.Helpers
open otsom.fs.Extensions
open otsom.fs.Telegram.Bot.Auth.Spotify.Settings

[<Literal>]
let private playlistTracksLimit = 100

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

let rec private listTracks' (client: ISpotifyClient) playlistId (offset: int) =
  async {
    let! tracks = client.Playlists.GetItems(playlistId, PlaylistGetItemsRequest(Offset = offset)) |> Async.AwaitTask

    return (tracks.Items |> Seq.map (fun x -> x.Track :?> FullTrack) |> getTracksIds, tracks.Total)
  }

let listPlaylistTracks (logger: ILogger) client : PlaylistRepo.ListTracks =
  fun playlistId ->
    let playlistId = playlistId |> PlaylistId.value
    let listPlaylistTracks = listTracks' client playlistId
    let loadTracks' = loadTracks' playlistTracksLimit

    task {
      try
        return! loadTracks' listPlaylistTracks

      with Spotify.ApiException e when e.Response.StatusCode = HttpStatusCode.NotFound ->
        Logf.logfw logger "Playlist with id %s{PlaylistId} not found in Spotify" playlistId

        return []
    }

let rec private listLikedTracks' (client: ISpotifyClient) (offset: int) =
  async {
    let! tracks = client.Library.GetTracks(LibraryTracksRequest(Offset = offset, Limit = 50)) |> Async.AwaitTask

    return (tracks.Items |> Seq.map _.Track |> getTracksIds, tracks.Total)
  }

let listSpotifyLikedTracks (client: ISpotifyClient) : UserRepo.ListLikedTracks =
    let likedTacksLimit = 50

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
