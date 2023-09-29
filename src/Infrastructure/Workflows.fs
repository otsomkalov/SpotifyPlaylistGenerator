namespace Infrastructure.Workflows

open System
open System.Collections.Generic
open System.Threading.Tasks
open Database.Entities
open MongoDB.Driver
open SpotifyAPI.Web
open Infrastructure
open System.Net
open System.Text.RegularExpressions
open Database
open Domain.Core
open Domain.Workflows
open Infrastructure.Core
open Infrastructure.Mapping
open System.Linq
open Infrastructure.Helpers
open Microsoft.Extensions.Logging
open StackExchange.Redis
open Infrastructure.Helpers.Spotify
open Domain.Extensions

[<RequireQualifiedAccess>]
module User =
  let rec private listLikedTracks' (client: ISpotifyClient) (offset: int) =
    async {
      let! tracks =
        client.Library.GetTracks(LibraryTracksRequest(Offset = offset, Limit = 50))
        |> Async.AwaitTask

      let! nextTracksIds =
        if isNull tracks.Next then
          [] |> async.Return
        else
          listLikedTracks' client (offset + 50)

      let currentTracksIds =
        tracks.Items |> Seq.map (fun x -> x.Track) |> Spotify.getTracksIds

      return List.append nextTracksIds currentTracksIds
    }

  let listLikedTracks (client: ISpotifyClient) : User.ListLikedTracks = listLikedTracks' client 0

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

  let create (db: IMongoDatabase) : User.Create =
    fun user ->
      task {
        let dbUser = user |> User.toDb
        let dbPresets = user.Presets |> Seq.map (SimplePreset.toFullDb user.Id)

        let usersCollection = db.GetCollection "users"
        let presetsCollection = db.GetCollection "presets"

        do! usersCollection.InsertOneAsync(dbUser)
        do! presetsCollection.InsertManyAsync(dbPresets)
      }

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
        |> Async.AwaitTask
      else
        let playlistAddItemsRequest = spotifyTracksIds |> PlaylistAddItemsRequest

        [ cache.ListLeftPushAsync(playlistId, (tracksIds |> List.map RedisValue |> Seq.toArray)) :> Task
          client.Playlists.AddItems(playlistId, playlistAddItemsRequest) :> Task ]
        |> Task.WhenAll
        |> Async.AwaitTask


[<RequireQualifiedAccess>]
module Playlist =
  let rec private listTracks' (client: ISpotifyClient) playlistId (offset: int) =
    async {
      let! tracks =
        client.Playlists.GetItems(playlistId, PlaylistGetItemsRequest(Offset = offset))
        |> Async.AwaitTask

      let! nextTracksIds =
        if isNull tracks.Next then
          [] |> async.Return
        else
          listTracks' client playlistId (offset + 100)

      let currentTracksIds =
        tracks.Items |> Seq.map (fun x -> x.Track :?> FullTrack) |> Spotify.getTracksIds

      return List.append nextTracksIds currentTracksIds
    }

  let listTracks (logger: ILogger) client : Playlist.ListTracks =
    fun playlistId ->
      async {
        try
          let playlistId = playlistId |> ReadablePlaylistId.value |> PlaylistId.value

          return! listTracks' client playlistId 0
        with ApiException e when e.Response.StatusCode = HttpStatusCode.NotFound ->
          logger.LogInformation(
            "Playlist with id {PlaylistId} not found in Spotify",
            playlistId |> ReadablePlaylistId.value |> PlaylistId.value
          )

          return []
      }

  let parseId: Playlist.ParseId =
    fun rawPlaylistId ->
      let getPlaylistIdFromUri (uri: Uri) = uri.Segments |> Array.last

      let (|Uri|_|) text =
        match Uri.TryCreate(text, UriKind.Absolute) with
        | true, uri -> Some uri
        | _ -> None

      let (|PlaylistId|_|) text =
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

  let checkPlaylistExistsInSpotify (client: ISpotifyClient) : Playlist.LoadFromSpotify =
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
              WriteableSpotifyPlaylist(
                { Id = playlist.Id |> PlaylistId
                  Name = playlist.Name }
              )
              |> SpotifyPlaylist.Writable

          return playlist |> Ok
        with ApiException e when e.Response.StatusCode = HttpStatusCode.NotFound ->
          return Playlist.MissingFromSpotifyError rawPlaylistId |> Error
      }

  let countTracks (connectionMultiplexer: IConnectionMultiplexer) : Playlist.CountTracks =
    let database = connectionMultiplexer.GetDatabase 0
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
