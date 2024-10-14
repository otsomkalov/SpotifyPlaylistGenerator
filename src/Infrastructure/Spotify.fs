module Infrastructure.Spotify

open System
open System.Collections.Generic
open System.Net
open System.Threading.Tasks
open Domain.Core
open Domain.Repos
open Domain.Workflows
open FSharp
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open SpotifyAPI.Web
open Infrastructure.Helpers
open otsom.fs.Core
open otsom.fs.Extensions
open otsom.fs.Telegram.Bot.Auth.Spotify.Settings
open otsom.fs.Telegram.Bot.Auth.Spotify.Workflows

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

let private loadTracks' limit loadBatch =
  async {
    let! initialBatch, totalCount = loadBatch 0

    return!
      match totalCount |> Option.ofNullable with
      | Some count ->
        [ limit..limit..count ]
        |> List.map (loadBatch >> Async.map fst)
        |> Async.Sequential
        |> Async.map (List.concat >> (List.append initialBatch))
      | None -> initialBatch |> async.Return
  }

let rec private listTracks' (client: ISpotifyClient) playlistId (offset: int) =
  async {
    let! tracks = client.Playlists.GetItems(playlistId, PlaylistGetItemsRequest(Offset = offset)) |> Async.AwaitTask

    return
        (tracks.Items
         |> Seq.choose (fun x ->
           match x.Track with
           | :? FullTrack as t -> Some t
           | _ -> None)
         |> getTracksIds,
         tracks.Total)
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

[<RequireQualifiedAccess>]
module internal Playlist =
  let private getSpotifyIds =
    fun tracksIds ->
      tracksIds |> List.map (fun id -> $"spotify:track:{id}") |> List<string>

  let addTracks (client: ISpotifyClient) =
    fun playlistId tracksIds ->
      client.Playlists.AddItems(playlistId, tracksIds |> getSpotifyIds |> PlaylistAddItemsRequest)
      |> Task.map ignore

  let replaceTracks (client: ISpotifyClient) =
    fun playlistId tracksIds ->
      client.Playlists.ReplaceItems(playlistId, tracksIds |> getSpotifyIds |> PlaylistReplaceItemsRequest)
      |> Task.ignore