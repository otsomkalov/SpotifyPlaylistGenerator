module Infrastructure.Repos

open Domain.Repos
open Domain.Workflows
open MongoDB.Driver
open Database
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

  let private listPlaylistsTracks (listTracks: Playlist.ListTracks) =
    List.map listTracks >> Task.WhenAll >> Task.map List.concat

  let listIncludedTracks logIncludedTracks (listTracks: Playlist.ListTracks) : PresetRepo.ListIncludedTracks =
    let listTracks = listPlaylistsTracks listTracks

    fun playlists ->
      task {
        let! playlistsTracks = playlists |> List.filter _.Enabled |> List.map _.Id |> listTracks

        logIncludedTracks playlistsTracks.Length

        return playlistsTracks
      }

  let listExcludedTracks logExcludedTracks (listTracks: Playlist.ListTracks) : PresetRepo.ListExcludedTracks =
    let listTracks = listPlaylistsTracks listTracks

    fun playlists ->
      task {
        let! playlistsTracks = playlists |> List.filter _.Enabled |> List.map _.Id |> listTracks

        logExcludedTracks playlistsTracks.Length

        return playlistsTracks
      }

[<RequireQualifiedAccess>]
module UserRepo =
  let load (db: IMongoDatabase) : UserRepo.Load =
    fun userId ->
      task {
        let collection = db.GetCollection "users"
        let id = userId |> UserId.value

        let usersFilter = Builders<Entities.User>.Filter.Eq((fun u -> u.Id), id)

        let! dbUser = collection.Find(usersFilter).SingleOrDefaultAsync()

        return User.fromDb dbUser
      }
