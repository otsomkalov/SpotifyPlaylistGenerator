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
open MusicPlatform
open SpotifyAPI.Web
open otsom.fs.Core
open otsom.fs.Extensions
open Infrastructure.Mapping
open System.Threading.Tasks
open Infrastructure.Cache
open MusicPlatform.Spotify

[<RequireQualifiedAccess>]
module PresetRepo =
  let internal load (collection: IMongoCollection<Entities.Preset>) =
    fun (PresetId presetId) ->
      task {
        let presetsFilter = Builders<Entities.Preset>.Filter.Eq((fun u -> u.Id), presetId)

        let! dbPreset = collection.Find(presetsFilter).SingleOrDefaultAsync()

        return dbPreset |> Preset.fromDb
      }

  let internal save (collection: IMongoCollection<Entities.Preset>) =
    fun preset ->
      task {
        let dbPreset = preset |> Preset.toDb

        let id = preset.Id |> PresetId.value

        let presetsFilter = Builders<Entities.Preset>.Filter.Eq((fun u -> u.Id), id)

        return! collection.ReplaceOneAsync(presetsFilter, dbPreset, ReplaceOptions(IsUpsert = true)) &|> ignore
      }

  let remove (db: IMongoDatabase) : PresetRepo.Remove =
    fun presetId ->
      let collection = db.GetCollection "presets"

      let id = presetId |> PresetId.value
      let presetsFilter = Builders<Entities.Preset>.Filter.Eq((fun u -> u.Id), id)

      collection.DeleteOneAsync(presetsFilter) |> Task.ignore

  let private listPlaylistsTracks (listTracks: Playlist.ListTracks) =
    List.map listTracks >> Task.WhenAll >> Task.map List.concat

  let listExcludedTracks logger (listTracks: Playlist.ListTracks) : PresetRepo.ListExcludedTracks =
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
    let listSpotifyTracks = User.listLikedTracks client
    let listRedisTracks = Redis.UserRepo.listLikedTracks telemetryClient multiplexer listSpotifyTracks userId

    Memory.UserRepo.listLikedTracks listRedisTracks

[<RequireQualifiedAccess>]
module TargetedPlaylistRepo =
  let private applyTracks spotifyAction cacheAction =
    fun (playlistId: PlaylistId) (tracks: Track list) ->
      let spotifyTask : Task<unit> = spotifyAction playlistId tracks
      let cacheTask: Task<unit> = cacheAction playlistId tracks

      Task.WhenAll([ spotifyTask; cacheTask])
      |> Task.ignore

  let addTracks (telemetryClient: TelemetryClient) (spotifyClient: ISpotifyClient) multiplexer : Playlist.AddTracks =
    let addInSpotify = Playlist.addTracks spotifyClient
    let addInCache = Redis.Playlist.appendTracks telemetryClient multiplexer

    applyTracks addInSpotify addInCache

  let replaceTracks (telemetryClient: TelemetryClient) (spotifyClient: ISpotifyClient) multiplexer : Playlist.ReplaceTracks =
    let replaceInSpotify = Playlist.replaceTracks spotifyClient
    let replaceInCache = Redis.Playlist.replaceTracks telemetryClient multiplexer

    fun playlistId tracks ->
      applyTracks replaceInSpotify replaceInCache playlistId tracks

[<RequireQualifiedAccess>]
module PlaylistRepo =
  let listTracks telemetryClient multiplexer logger client : Playlist.ListTracks =
    let listCachedPlaylistTracks = Redis.Playlist.listTracks telemetryClient multiplexer
    let listSpotifyPlaylistTracks = Playlist.listTracks logger client
    let cachePlaylistTracks = Redis.Playlist.replaceTracks telemetryClient multiplexer

    fun playlistId ->
      listCachedPlaylistTracks playlistId
      |> Task.bind (function
        | [] ->
          listSpotifyPlaylistTracks playlistId
          |> Task.taskTap (cachePlaylistTracks playlistId)
        | tracks -> Task.FromResult tracks)

type PresetRepo(db: IMongoDatabase) =
  let collection = db.GetCollection<Entities.Preset> "presets"

  interface IPresetRepo with
    member this.LoadPreset(presetId) =
      PresetRepo.load collection presetId
    member this.SavePreset(preset) =
      PresetRepo.save collection preset