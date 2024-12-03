namespace Domain.Integrations.Spotify

open System
open System.Collections.Generic
open System.Net
open System.Threading.Tasks
open Domain.Core
open Domain.Repos
open Microsoft.Extensions.Options
open MusicPlatform
open SpotifyAPI.Web
open otsom.fs.Core
open otsom.fs.Extensions
open Domain.Integrations.Spotify.Helpers
open otsom.fs.Telegram.Bot.Auth.Spotify.Settings
open otsom.fs.Telegram.Bot.Auth.Spotify.Workflows

type GetClient = UserId -> Task<ISpotifyClient option>

module Core =
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
module PlaylistRepo =
  let loadTracks' limit loadBatch = async {
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

  let rec listTracks' (client: ISpotifyClient) playlistId (offset: int) = async {
    let! tracks =
      client.Playlists.GetItems(playlistId, PlaylistGetItemsRequest(Offset = offset))
      |> Async.AwaitTask

    return
      (tracks.Items
       |> Seq.choose (fun x ->
         match x.Track with
         | :? FullTrack as t -> Some t
         | _ -> None)
       |> getTracksIds,
       tracks.Total)
  }

  let load (client: ISpotifyClient) : PlaylistRepo.Load =
    fun (PlaylistId playlistId) ->
      task {
        try
          let! playlist = playlistId |> client.Playlists.Get

          let! currentUser = client.UserProfile.Current()

          let playlist =
            if playlist.Owner.Id = currentUser.Id then
              SpotifyPlaylist.Writable(
                { Id = playlist.Id |> PlaylistId
                  Name = playlist.Name }
              )
            else
              SpotifyPlaylist.Readable(
                { Id = playlist.Id |> PlaylistId
                  Name = playlist.Name }
              )

          return playlist |> Ok
        with ApiException e when e.Response.StatusCode = HttpStatusCode.NotFound ->
          return Playlist.MissingFromSpotifyError playlistId |> Error
      }