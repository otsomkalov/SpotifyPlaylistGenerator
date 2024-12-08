namespace MusicPlatform.Spotify

open System
open System.Collections.Concurrent
open System.Net
open System.Text.RegularExpressions
open FSharp
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Microsoft.FSharp.Control
open MusicPlatform
open SpotifyAPI.Web
open MusicPlatform.Spotify.Helpers
open otsom.fs.Extensions
open System.Collections.Generic
open otsom.fs.Telegram.Bot.Auth.Spotify.Settings
open otsom.fs.Telegram.Bot.Auth.Spotify.Workflows
open System.Threading.Tasks

module Core =
  type GetClient = UserId -> Task<ISpotifyClient option>

  let getClient (loadCompletedAuth: Completed.Load) (spotifyOptions: IOptions<SpotifySettings>) : GetClient =
    let spotifySettings = spotifyOptions.Value
    let clients = Dictionary<string, ISpotifyClient>()

    fun (UserId userId) ->
      match clients.TryGetValue(userId) with
      | true, client -> client |> Some |> Task.FromResult
      | false, _ ->
        userId
        |> (fun userId -> otsom.fs.Core.UserId(userId |> int64))
        |> loadCompletedAuth
        |> TaskOption.taskMap (fun auth -> task {
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
module Playlist =
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

  let rec private listTracks' (client: ISpotifyClient) playlistId (offset: int) = async {
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

  let listTracks (logger: ILogger) client : Playlist.ListTracks =
    let playlistTracksLimit = 100

    fun (PlaylistId playlistId) ->
      let listPlaylistTracks = listTracks' client playlistId
      let loadTracks' = loadTracks' playlistTracksLimit

      task {
        try
          return! loadTracks' listPlaylistTracks

        with ApiException e when e.Response.StatusCode = HttpStatusCode.NotFound ->
          Logf.logfw logger "Playlist with id %s{PlaylistId} not found in Spotify" playlistId

          return []
      }

  let private getSpotifyIds =
    fun (tracks: Track list) ->
      tracks
      |> List.map (_.Id)
      |> List.map (fun (TrackId id) -> $"spotify:track:{id}")
      |> List<string>

  let addTracks (client: ISpotifyClient) : Playlist.AddTracks =
    fun (PlaylistId playlistId) tracks ->
      client.Playlists.AddItems(playlistId, tracks |> getSpotifyIds |> PlaylistAddItemsRequest)
      &|> ignore

  let replaceTracks (client: ISpotifyClient) : Playlist.ReplaceTracks =
    fun (PlaylistId playlistId) tracks ->
      client.Playlists.ReplaceItems(playlistId, tracks |> getSpotifyIds |> PlaylistReplaceItemsRequest)
      &|> ignore

  let load (client: ISpotifyClient) : Playlist.Load =
    fun (PlaylistId playlistId) -> task {
      try
        let! playlist = playlistId |> client.Playlists.Get

        let! currentUser = client.UserProfile.Current()

        let playlist =
          if playlist.Owner.Id = currentUser.Id then
            Writable(
              { Id = playlist.Id |> PlaylistId
                Name = playlist.Name }
            )
          else
            Readable(
              { Id = playlist.Id |> PlaylistId
                Name = playlist.Name }
            )

        return playlist |> Ok
      with ApiException e when e.Response.StatusCode = HttpStatusCode.NotFound ->
        return Playlist.LoadError.NotFound |> Error
    }

  let parseId: Playlist.ParseId =
    fun (Playlist.RawPlaylistId rawPlaylistId) ->
      let getPlaylistIdFromUri (uri: Uri) = uri.Segments |> Array.last

      let (|Uri|_|) text =
        match Uri.TryCreate(text, UriKind.Absolute) with
        | true, uri -> Some uri
        | _ -> None

      let (|PlaylistId|_|) (text: string) =
        if Regex.IsMatch(text, "^[A-z0-9]{22}$") then
          Some text
        else
          None

      let (|SpotifyUri|_|) (text: string) =
        match text.Split(":") with
        | [| "spotify"; "playlist"; id |] -> Some(id)
        | _ -> None

      match rawPlaylistId with
      | SpotifyUri id -> id |> PlaylistId |> Ok
      | Uri uri -> uri |> getPlaylistIdFromUri |> PlaylistId |> Ok
      | PlaylistId id -> id |> PlaylistId |> Ok
      | id -> Playlist.IdParsingError(id) |> Error

[<RequireQualifiedAccess>]
module User =
  let rec private listLikedTracks' (client: ISpotifyClient) (offset: int) = async {
    let! tracks =
      client.Library.GetTracks(LibraryTracksRequest(Offset = offset, Limit = 50))
      |> Async.AwaitTask

    return (tracks.Items |> Seq.map _.Track |> getTracksIds, tracks.Total)
  }

  let listLikedTracks (client: ISpotifyClient) : User.ListLikedTracks =
    let likedTacksLimit = 50

    let listLikedTracks' = listLikedTracks' client
    let loadTracks' = Playlist.loadTracks' likedTacksLimit

    fun () -> loadTracks' listLikedTracks' |> Async.StartAsTask

[<RequireQualifiedAccess>]
module Track =
  let getRecommendations (client: ISpotifyClient) : Track.GetRecommendations =
    let recommendationsLimit = 100

    fun tracks ->
      let request = RecommendationsRequest()

      for track in tracks |> List.takeSafe 5 do
        request.SeedTracks.Add(track |> TrackId.value)

      request.Limit <- recommendationsLimit

      client.Browse.GetRecommendations(request)
      |> Task.map _.Tracks
      |> Task.map (
        Seq.map (fun st ->
          { Id = TrackId st.Id
            Artists = st.Artists |> Seq.map (fun a -> { Id = ArtistId a.Id }) |> Set.ofSeq })
        >> Seq.toList
      )

module Library =
  let buildMusicPlatform (getSpotifyClient: Core.GetClient) : BuildMusicPlatform =
    fun userId -> userId |> getSpotifyClient &|> Option.map (fun _ -> { new IMusicPlatform })
