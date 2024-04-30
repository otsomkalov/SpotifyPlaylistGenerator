module Infrastructure.Repos

open Domain.Repos
open Domain.Workflows
open MongoDB.Driver
open Database
open otsom.fs.Extensions
open otsom.fs.Telegram.Bot.Core
open Infrastructure.Mapping

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

  let remove (db: IMongoDatabase) : PresetRepo.Remove =
    fun presetId ->
      let collection = db.GetCollection "presets"

      let id = presetId |> PresetId.value
      let presetsFilter = Builders<Entities.Preset>.Filter.Eq((fun u -> u.Id), id)

      collection.DeleteOneAsync(presetsFilter) |> Task.ignore

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
