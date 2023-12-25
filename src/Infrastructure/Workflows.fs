namespace Infrastructure.Workflows

open System
open System.Collections.Generic
open System.Threading.Tasks
open Infrastructure
open Infrastructure.Settings
open Microsoft.Extensions.Options
open MongoDB.Driver
open SpotifyAPI.Web
open System.Net
open System.Text.RegularExpressions
open Database
open Domain.Core
open Domain.Workflows
open Infrastructure.Core
open Infrastructure.Mapping
open StackExchange.Redis
open Infrastructure.Helpers.Spotify
open otsom.FSharp.Extensions

[<RequireQualifiedAccess>]
module User =
  let load (db: IMongoDatabase) : User.Load =
    fun userId ->
      task {
        let collection = db.GetCollection "users"
        let id = userId |> UserId.value

        let usersFilter = Builders<Entities.User>.Filter.Eq((fun u -> u.Id), id)

        let! dbUser = collection.Find(usersFilter).SingleOrDefaultAsync()

        return User.fromDb dbUser
      }

  let update (db: IMongoDatabase) : User.Update =
    fun user ->
      task {
        let collection = db.GetCollection "users"
        let id = user.Id |> UserId.value

        let usersFilter = Builders<Entities.User>.Filter.Eq((fun u -> u.Id), id)

        let dbUser = user |> User.toDb

        return! collection.ReplaceOneAsync(usersFilter, dbUser) |> Task.map ignore
      }

  let exists (db: IMongoDatabase) : User.Exists =
    fun userId ->
      task {
        let collection = db.GetCollection "users"
        let id = userId |> UserId.value

        let usersFilter = Builders<Entities.User>.Filter.Eq((fun u -> u.Id), id)

        let! dbUser = collection.Find(usersFilter).SingleOrDefaultAsync()

        return not (isNull dbUser)
      }

  let createIfNotExists (db: IMongoDatabase) : User.CreateIfNotExists =
    fun userId ->
      userId
      |> (exists db)
      |> Task.bind (function
        | true -> Task.FromResult()
        | false ->
          task {
            let user = User.create userId

            let dbUser = user |> User.toDb

            let usersCollection = db.GetCollection "users"

            do! usersCollection.InsertOneAsync(dbUser)
          })

[<RequireQualifiedAccess>]
module TargetedPlaylist =
  let updateTracks (cache: IDatabase) (client: ISpotifyClient) : Playlist.UpdateTracks =
    fun playlist tracksIds ->
      let tracksIds = tracksIds |> List.map TrackId.value
      let playlistId = playlist.Id |> WritablePlaylistId.value |> PlaylistId.value

      let spotifyTracksIds =
        tracksIds |> List.map (fun id -> $"spotify:track:{id}") |> List<string>

      if playlist.Overwrite then
        task {

          let transaction = cache.CreateTransaction()

          let deleteTask = transaction.KeyDeleteAsync(playlistId) :> Task

          let addTask =
            transaction.ListLeftPushAsync(playlistId, (tracksIds |> List.map RedisValue |> Seq.toArray)) :> Task

          let expireTask = transaction.KeyExpireAsync(playlistId, TimeSpan.FromDays(7))

          let! _ = transaction.ExecuteAsync()

          let! _ = deleteTask
          let! _ = addTask
          let! _ = expireTask

          let! _ = client.Playlists.ReplaceItems(playlistId, PlaylistReplaceItemsRequest spotifyTracksIds)

          ()
        }
      else
        let playlistAddItemsRequest = spotifyTracksIds |> PlaylistAddItemsRequest

        [ cache.ListLeftPushAsync(playlistId, (tracksIds |> List.map RedisValue |> Seq.toArray)) |> Task.map ignore
          client.Playlists.AddItems(playlistId, playlistAddItemsRequest) |> Task.map ignore ]
        |> Task.WhenAll
        |> Task.map ignore

[<RequireQualifiedAccess>]
module Playlist =
  let parseId: Playlist.ParseId =
    fun rawPlaylistId ->
      let getPlaylistIdFromUri (uri: Uri) = uri.Segments |> Array.last

      let (|Uri|_|) text =
        match Uri.TryCreate(text, UriKind.Absolute) with
        | true, uri -> Some uri
        | _ -> None

      let (|PlaylistId|_|) (text: string) =
        if Regex.IsMatch(text, "[A-z0-9]{22}") then
          Some text
        else
          None

      let (|SpotifyUri|_|) (text: string) =
        match text.Split(":") with
        | [| "spotify"; "playlist"; id |] -> Some(id)
        | _ -> None

      match rawPlaylistId |> RawPlaylistId.value with
      | SpotifyUri id -> id |> Playlist.ParsedPlaylistId |> Ok
      | Uri uri -> uri |> getPlaylistIdFromUri |> Playlist.ParsedPlaylistId |> Ok
      | PlaylistId id -> id |> Playlist.ParsedPlaylistId |> Ok
      | _ -> Playlist.IdParsingError() |> Error

  let loadFromSpotify (client: ISpotifyClient) : Playlist.LoadFromSpotify =
    fun playlistId ->
      let rawPlaylistId = playlistId |> ParsedPlaylistId.value

      task {
        try
          let! playlist = rawPlaylistId |> client.Playlists.Get

          let! currentUser = client.UserProfile.Current()

          let playlist =
            if playlist.Owner.Id = currentUser.Id then
              WriteableSpotifyPlaylist(
                { Id = playlist.Id |> PlaylistId
                  Name = playlist.Name }
              )
              |> SpotifyPlaylist.Writable
            else
              ReadableSpotifyPlaylist(
                { Id = playlist.Id |> PlaylistId
                  Name = playlist.Name }
              )
              |> SpotifyPlaylist.Readable

          return playlist |> Ok
        with ApiException e when e.Response.StatusCode = HttpStatusCode.NotFound ->
          return Playlist.MissingFromSpotifyError rawPlaylistId |> Error
      }

  let countTracks (connectionMultiplexer: IConnectionMultiplexer) : Playlist.CountTracks =
    let database = connectionMultiplexer.GetDatabase Cache.playlistsDatabase
    PlaylistId.value >> RedisKey >> database.ListLengthAsync

[<RequireQualifiedAccess>]
module Preset =
  let load (db: IMongoDatabase) : Preset.Load =
    fun presetId ->
      task {
        let collection = db.GetCollection "presets"

        let id = presetId |> PresetId.value

        let presetsFilter = Builders<Entities.Preset>.Filter.Eq((fun u -> u.Id), id)

        let! dbPreset = collection.Find(presetsFilter).SingleOrDefaultAsync()

        return dbPreset |> Preset.fromDb
      }

  let update (db: IMongoDatabase) : Preset.Update =
    fun preset ->
      task {
        let collection = db.GetCollection "presets"

        let dbPreset = preset |> Preset.toDb

        let id = preset.Id |> PresetId.value

        let presetsFilter = Builders<Entities.Preset>.Filter.Eq((fun u -> u.Id), id)

        return! collection.ReplaceOneAsync(presetsFilter, dbPreset) |> Task.map ignore
      }

  let save (db: IMongoDatabase) : Preset.Save =
    fun preset ->
      task {
        let collection = db.GetCollection "presets"

        let dbPreset = preset |> Preset.toDb

        return! collection.InsertOneAsync(dbPreset)
      }

  let remove (db: IMongoDatabase) : Preset.Remove =
    fun presetId ->
      task{
        let collection = db.GetCollection "presets"

        let id = presetId |> PresetId.value
        let presetsFilter = Builders<Entities.Preset>.Filter.Eq((fun u -> u.Id), id)

        let! dbPreset = collection.FindOneAndDeleteAsync(presetsFilter)

        return dbPreset |> Preset.fromDb
      }

  let private listPlaylistsTracks (listTracks: Playlist.ListTracks) =
    List.map listTracks
    >> Task.WhenAll
    >> Task.map List.concat

  let listIncludedTracks logIncludedTracks (listTracks: Playlist.ListTracks) : Preset.ListIncludedTracks =
    let listTracks = listPlaylistsTracks listTracks

    fun playlists ->
      task{
        let! playlistsTracks =
          playlists
          |> List.filter (fun p -> p.Enabled)
          |> List.map (fun p -> p.Id)
          |> listTracks

        logIncludedTracks playlistsTracks.Length

        return playlistsTracks
      }

  let listExcludedTracks logExcludedTracks (listTracks: Playlist.ListTracks) : Preset.ListExcludedTracks =
    let listTracks = listPlaylistsTracks listTracks

    fun playlists ->
      task{
        let! playlistsTracks =
          playlists
          |> List.filter (fun p -> p.Enabled)
          |> List.map (fun p -> p.Id)
          |> listTracks

        logExcludedTracks playlistsTracks.Length

        return playlistsTracks
      }

[<RequireQualifiedAccess>]
module Auth =
  let initState (connectionMultiplexer: IConnectionMultiplexer) : Auth.InitState =
    let database = connectionMultiplexer.GetDatabase Cache.authDatabase

    fun userId ->
      task {
        let userId = userId |> UserId.value |> string
        let state = Auth.State.create()
        let stateValue = state |> Auth.State.value

        do! database.HashSetAsync(stateValue, [| HashEntry("UserId", userId) |])
        do! database.KeyExpireAsync(stateValue, TimeSpan.FromMinutes(5)) |> Task.map ignore

        return state
      }

  let tryGetInitedAuth (connectionMultiplexer: IConnectionMultiplexer) : Auth.TryGetInitedAuth =
    let database = connectionMultiplexer.GetDatabase Cache.authDatabase

    fun state ->
      state
      |> Auth.State.value
      |> Task.FromResult
      |> Task.bind (fun s -> database.HashGetAsync(s, "UserId"))
      |> Task.map (fun s ->
        if s.IsNullOrEmpty then
          None
        else
          Some(
            { State = state
              UserId = (s |> int64 |> UserId) }
          ))

  let saveFulfilledAuth (connectionMultiplexer: IConnectionMultiplexer) : Auth.SaveFulfilledAuth =
    let database = connectionMultiplexer.GetDatabase Cache.authDatabase

    fun auth ->
      task {
        let state = auth.State |> Auth.State.value

        let hashEntries =
          [| HashEntry("UserId", (auth.UserId |> UserId.value |> string |> RedisValue))
             HashEntry("Code", auth.Code) |]

        do! database.HashSetAsync(state, hashEntries)
      }

  let getLoginLink (spotifyOptions: IOptions<SpotifySettings>): Auth.GetLoginLink =
    fun state ->
      let spotifySettings = spotifyOptions.Value

      let scopes =
        [ Scopes.PlaylistModifyPrivate
          Scopes.PlaylistModifyPublic
          Scopes.UserLibraryRead ]
        |> List<string>

      let loginRequest =
        LoginRequest(spotifySettings.CallbackUrl, spotifySettings.ClientId, LoginRequest.ResponseType.Code, Scope = scopes, State = (state |> Auth.State.value))

      loginRequest.ToUri().ToString()

  let tryGetCompletedAuth (connectionMultiplexer: IConnectionMultiplexer) : Auth.TryGetCompletedAuth =
    let database = connectionMultiplexer.GetDatabase Cache.authDatabase

    fun state ->
      state
      |> Auth.State.value
      |> Task.FromResult
      |> Task.bind (fun s -> database.HashGetAllAsync(s))
      |> Task.map (fun entries ->
        match entries with
        | [|codeEntry; userIdEntry|] when codeEntry.Name = "Code" && userIdEntry.Name = "UserId" ->
          Some(
            { UserId = (userIdEntry.Value |> int64 |> UserId)
              State = state
              Code = codeEntry.Value }
          )
        | _ -> None)

  let getToken (spotifyOptions: IOptions<SpotifySettings>) : Auth.GetToken =
    let spotifySettings = spotifyOptions.Value

    fun code ->
      (spotifySettings.ClientId, spotifySettings.ClientSecret, code, spotifySettings.CallbackUrl)
      |> AuthorizationCodeTokenRequest
      |> OAuthClient().RequestToken
      |> Task.map (fun r -> r.RefreshToken)

  let saveCompletedAuth (connectionMultiplexer: IConnectionMultiplexer) : Auth.SaveCompletedAuth =
    let database = connectionMultiplexer.GetDatabase Cache.tokensDatabase

    fun auth ->
      task {
        do! database.StringSetAsync((auth.UserId |> UserId.value |> string), auth.Token, (TimeSpan.FromDays 7))
          |> Task.map ignore
      }