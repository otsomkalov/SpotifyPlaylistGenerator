module Infrastructure.Repos

open Domain.Repos
open Domain.Workflows
open MongoDB.Driver
open Database
open otsom.fs.Extensions

[<RequireQualifiedAccess>]
module PresetRepo =
  let remove (db: IMongoDatabase) : PresetRepo.Remove =
    fun presetId ->
      let collection = db.GetCollection "presets"

      let id = presetId |> PresetId.value
      let presetsFilter = Builders<Entities.Preset>.Filter.Eq((fun u -> u.Id), id)

      collection.DeleteOneAsync(presetsFilter) |> Task.ignore