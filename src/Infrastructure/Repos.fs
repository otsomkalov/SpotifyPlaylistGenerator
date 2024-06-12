module Infrastructure.Repos

open System
open System.Collections.Generic
open Domain.Core
open Domain.Repos
open Domain.Workflows
open FSharp
open Infrastructure.Helpers
open MongoDB.Driver
open Database
open SpotifyAPI.Web
open StackExchange.Redis
open otsom.fs.Extensions
open otsom.fs.Telegram.Bot.Core
open Infrastructure.Mapping
open System.Threading.Tasks

[<RequireQualifiedAccess>]
module PresetRepo =
  let load (db: IMongoDatabase) : PresetRepo.Load =
    fun presetId ->
      task {
        let collection = db.GetCollection "presets"

        let id = presetId |> PresetId.value

        let presetsFilter = Builders<Entities.Preset>.Filter.Eq((fun u -> u.Id), id)

        let! dbPreset = collection.Find(presetsFilter).SingleOrDefaultAsync()

        return dbPreset |> Preset.fromDb
      }

  let update (db: IMongoDatabase) : PresetRepo.Update =
    fun preset ->
      task {
        let collection = db.GetCollection "presets"

        let dbPreset = preset |> Preset.toDb

        let id = preset.Id |> PresetId.value

        let presetsFilter = Builders<Entities.Preset>.Filter.Eq((fun u -> u.Id), id)

        return! collection.ReplaceOneAsync(presetsFilter, dbPreset) |> Task.map ignore
      }

  let remove (db: IMongoDatabase) : PresetRepo.Remove =
    fun presetId ->
      let collection = db.GetCollection "presets"

      let id = presetId |> PresetId.value
      let presetsFilter = Builders<Entities.Preset>.Filter.Eq((fun u -> u.Id), id)

      collection.DeleteOneAsync(presetsFilter) |> Task.ignore

  let private listPlaylistsTracks (listTracks: PlaylistRepo.ListTracks) =
    List.map listTracks >> Task.WhenAll >> Task.map List.concat

  let listIncludedTracks logger (listTracks: PlaylistRepo.ListTracks) : PresetRepo.ListIncludedTracks =
    let listTracks = listPlaylistsTracks listTracks

    fun playlists ->
      task {
        let! playlistsTracks = playlists |> List.filter _.Enabled |> List.map (_.Id >> ReadablePlaylistId.value) |> listTracks

        Logf.logfi logger "Preset has %i{IncludedTracksCount} included tracks" playlistsTracks.Length

        return playlistsTracks
      }

  let listExcludedTracks logger (listTracks: PlaylistRepo.ListTracks) : PresetRepo.ListExcludedTracks =
    let listTracks = listPlaylistsTracks listTracks

    fun playlists ->
      task {
        let! playlistsTracks = playlists |> List.filter _.Enabled |> List.map (_.Id >> ReadablePlaylistId.value) |> listTracks

        Logf.logfi logger "Preset has %i{ExcludedTracksCount} excluded tracks" playlistsTracks.Length

        return playlistsTracks
      }

[<RequireQualifiedAccess>]
module UserRepo =
  let load (db: IMongoDatabase) : UserRepo.Load =
    fun userId ->
      let collection = db.GetCollection "users"
      let id = userId |> UserId.value

      let usersFilter = Builders<Entities.User>.Filter.Eq((fun u -> u.Id), id)

      collection.Find(usersFilter).SingleOrDefaultAsync() |> Task.map User.fromDb

  let update (db: IMongoDatabase) : UserRepo.Update =
    fun user ->
      let collection = db.GetCollection "users"
      let id = user.Id |> UserId.value

      let usersFilter = Builders<Entities.User>.Filter.Eq((fun u -> u.Id), id)

      let dbUser = user |> User.toDb

      collection.ReplaceOneAsync(usersFilter, dbUser) |> Task.map ignore

  let exists (db: IMongoDatabase) : UserRepo.Exists =
    fun userId ->
      let collection = db.GetCollection "users"
      let id = userId |> UserId.value

      let usersFilter = Builders<Entities.User>.Filter.Eq((fun u -> u.Id), id)

      collection.CountDocumentsAsync(usersFilter) |> Task.map ((<) 0)

  let create (db: IMongoDatabase) : UserRepo.Create =
    fun user ->
      let collection = db.GetCollection "users"
      let dbUser = user |> User.toDb

      task { do! collection.InsertOneAsync(dbUser) }

  let listLikedTracks telemetryClient cache client logger userId : UserRepo.ListLikedTracks =
    let listCachedTracks = Cache.listLikedTracks telemetryClient cache userId
    let listSpotifyTracks = Spotify.listSpotifyLikedTracks client
    let cacheUserTracks = Cache.cacheUserTracks telemetryClient cache userId

    fun () ->
      listCachedTracks ()
      |> Task.bind (function
        | [] ->
          listSpotifyTracks ()
          |> Task.taskTap cacheUserTracks
        | tracks -> Task.FromResult tracks)
      |> Task.tap (fun tracks ->
        Logf.logfi logger "User %i{TelegramId} has %i{LikedTracksCount} liked tracks" (userId |> UserId.value) tracks.Length)

[<RequireQualifiedAccess>]
module TargetedPlaylistRepo =
  let appendTracksInSpotify (client: ISpotifyClient) : TargetedPlaylistRepo.AppendTracks =
    fun playlistId tracks ->
      let tracksIds = tracks |> List.map (_.Id >> TrackId.value)
      let playlistId = playlistId |> WritablePlaylistId.value |> PlaylistId.value

      let spotifyTracksIds =
        tracksIds |> List.map (fun id -> $"spotify:track:{id}") |> List<string>

      client.Playlists.AddItems(playlistId, PlaylistAddItemsRequest spotifyTracksIds)
      |> Task.map ignore

  let replaceTracksInSpotify (client: ISpotifyClient) : TargetedPlaylistRepo.ReplaceTracks =
    fun playlistId tracks ->
      let tracksIds = tracks |> List.map (_.Id >> TrackId.value)
      let playlistId = playlistId |> WritablePlaylistId.value |> PlaylistId.value

      let spotifyTracksIds =
        tracksIds |> List.map (fun id -> $"spotify:track:{id}") |> List<string>

      client.Playlists.ReplaceItems(playlistId, PlaylistReplaceItemsRequest spotifyTracksIds)
      |> Task.ignore

  let private serializeTracks tracks =
    tracks |> List.map JSON.serialize |> List.map RedisValue |> Seq.toArray

  let appendTracksInCache (cache: IDatabase) : TargetedPlaylistRepo.AppendTracks =
    fun playlistId tracks ->
      let playlistId = playlistId |> WritablePlaylistId.value |> PlaylistId.value
      let serializedTracks = serializeTracks tracks

      cache.ListLeftPushAsync(playlistId, serializedTracks) |> Task.map ignore

  let replaceTracksInCache (cache: IDatabase) : TargetedPlaylistRepo.ReplaceTracks =
    fun playlistId tracks ->
      let playlistId = playlistId |> WritablePlaylistId.value |> PlaylistId.value
      let serializedTracks = serializeTracks tracks

      let transaction = cache.CreateTransaction()

      let _ = transaction.KeyDeleteAsync(playlistId)
      let _ = transaction.ListLeftPushAsync(playlistId, serializedTracks)
      let _ = transaction.KeyExpireAsync(playlistId, TimeSpan.FromDays(7))

      transaction.ExecuteAsync() |> Task.map ignore

[<RequireQualifiedAccess>]
module TrackRepo =
  [<Literal>]
  let private recommendationsLimit = 100

  let getRecommendations logRecommendedTracks (client: ISpotifyClient) : TrackRepo.GetRecommendations =
    fun tracks ->
      let request = RecommendationsRequest()

      for track in tracks |> List.takeSafe 5 do
        request.SeedTracks.Add(track |> TrackId.value)

      request.Limit <- recommendationsLimit

      client.Browse.GetRecommendations(request)
      |> Task.map _.Tracks
      |> Task.tap (fun tracks -> logRecommendedTracks tracks.Count)
      |> Task.map (
        Seq.map (fun st ->
          { Id = TrackId st.Id
            Artists = st.Artists |> Seq.map (fun a -> { Id = ArtistId a.Id }) |> Set.ofSeq })
        >> Seq.toList
      )

[<RequireQualifiedAccess>]
module PlaylistRepo =
  let listTracks telemetryClient cache logger client : PlaylistRepo.ListTracks =
    fun playlistId ->
      Cache.listPlaylistTracks telemetryClient cache playlistId
      |> Task.bind (function
        | [] ->
          Spotify.listPlaylistTracks logger client playlistId
          |> Task.taskTap (Cache.cachePlaylistTracks telemetryClient cache playlistId)
        | tracks -> Task.FromResult tracks)
