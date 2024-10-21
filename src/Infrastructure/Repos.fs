module Infrastructure.Repos

open Azure.Storage.Queues
open Domain.Core
open Domain.Repos
open Domain.Workflows
open FSharp
open Infrastructure.Helpers
open Microsoft.ApplicationInsights
open MongoDB.Driver
open Database
open SpotifyAPI.Web
open otsom.fs.Core
open otsom.fs.Extensions
open Infrastructure.Mapping
open System.Threading.Tasks
open Infrastructure.Cache

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

  let queueRun (queueClient: QueueClient) : UserId -> PresetRepo.QueueRun =
    fun userId ->
      fun presetId ->
        {| UserId = userId
           PresetId = presetId |}
        |> JSON.serialize
        |> queueClient.SendMessageAsync
        |> Task.map ignore

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

  let listLikedTracks telemetryClient multiplexer client userId : UserRepo.ListLikedTracks =
    let listSpotifyTracks = Spotify.listSpotifyLikedTracks client
    let listRedisTracks = Redis.UserRepo.listLikedTracks telemetryClient multiplexer listSpotifyTracks userId

    Memory.UserRepo.listLikedTracks listRedisTracks

[<RequireQualifiedAccess>]
module TargetedPlaylistRepo =
  let private applyTracks spotifyAction cacheAction =
    fun playlistId (tracks: Track list) ->
      let playlistId = playlistId |> WritablePlaylistId.value |> PlaylistId.value

      let spotifyTask : Task<unit> = spotifyAction playlistId (tracks |> List.map (_.Id >> TrackId.value))
      let cacheTask: Task<unit> = cacheAction playlistId tracks

      Task.WhenAll([ spotifyTask; cacheTask])
      |> Task.ignore

  let appendTracks (telemetryClient: TelemetryClient) (spotifyClient: ISpotifyClient) multiplexer : TargetedPlaylistRepo.AppendTracks =
    let addInSpotify = Spotify.Playlist.addTracks spotifyClient
    let addInCache = Redis.Playlist.appendTracks telemetryClient multiplexer

    applyTracks addInSpotify addInCache

  let replaceTracks (telemetryClient: TelemetryClient) (spotifyClient: ISpotifyClient) multiplexer : TargetedPlaylistRepo.ReplaceTracks =
    let replaceInSpotify = Spotify.Playlist.replaceTracks spotifyClient
    let replaceInCache = Redis.Playlist.replaceTracks telemetryClient multiplexer

    applyTracks replaceInSpotify replaceInCache

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
  let listTracks telemetryClient multiplexer logger client : PlaylistRepo.ListTracks =
    let listCachedPlaylistTracks = Redis.Playlist.listTracks telemetryClient multiplexer
    let listSpotifyPlaylistTracks = Spotify.listPlaylistTracks logger client
    let cachePlaylistTracks = Redis.Playlist.replaceTracks telemetryClient multiplexer

    fun playlistId ->
      listCachedPlaylistTracks playlistId
      |> Task.bind (function
        | [] ->
          listSpotifyPlaylistTracks playlistId
          |> Task.taskTap (cachePlaylistTracks (playlistId |> PlaylistId.value))
        | tracks -> Task.FromResult tracks)
