open System
open System.Threading.Tasks
open Database.Entities
open MongoDB.Driver
open Npgsql.FSharp
open shortid
open shortid.Configuration

module Task =
  let map mapping task' =
    task {
      let! value = task'

      return mapping value
    }

  let bind (binder: 'a -> Task<'b>) (taskResult: Task<'a>) : Task<'b> =
    task {
      let! result = taskResult

      return! binder result
    }

module PG =

  let private readPreset (reader: RowReader) =
    {| Id = reader.int "Id"
       Name = reader.string "Name"
       UserId = reader.int64 "UserId"
       Settings =
        {| IncludeLikedTracks = reader.boolOrNone "Settings_IncludeLikedTracks"
           PlaylistSize = reader.int "Settings_PlaylistSize"
           RecommendationsEnabled = reader.bool "Settings_RecommendationsEnabled" |} |}

  let private readIncludedPlaylist (reader: RowReader) =
    {| Id = reader.string "Url"
       Name = reader.string "Name"
       Disabled = reader.bool "Disabled"
       PresetId = reader.int "PresetId" |}

  let private readExcludedPlaylist (reader: RowReader) =
    {| Id = reader.string "Url"
       Name = reader.string "Name"
       Disabled = reader.bool "Disabled"
       PresetId = reader.int "PresetId" |}

  let private readTargetedPlaylist (reader: RowReader) =
    {| Id = reader.string "Url"
       Name = reader.string "Name"
       Disabled = reader.bool "Disabled"
       Overwrite = reader.bool "Overwrite"
       PresetId = reader.int "PresetId" |}

  let private readUser (reader: RowReader) = {| Id = reader.int64 "Id" |}

  let private loadIncludedPlaylists connectionString presetId =
    task {
      let! includedPlaylists =
        connectionString
        |> Sql.connect
        |> Sql.query
          $"SELECT * FROM \"spotify-playlist-generator\".public.\"Playlists\" as P where P.\"PlaylistType\" = 0 and P.\"PresetId\" = {presetId}"
        |> Sql.executeAsync readIncludedPlaylist
        |> Task.map (List.map (fun ip -> IncludedPlaylist(Id = ip.Id, Name = ip.Name, Disabled = ip.Disabled)))

      printfn "Loaded %i included playlists for preset with id %i" (includedPlaylists |> List.length) presetId

      return includedPlaylists
    }

  let private loadExcludedPlaylists connectionString presetId =
    task {
      let! excludedPlaylists =
        connectionString
        |> Sql.connect
        |> Sql.query
          $"SELECT * FROM \"spotify-playlist-generator\".public.\"Playlists\" as P where P.\"PlaylistType\" = 1 and P.\"PresetId\" = {presetId}"
        |> Sql.executeAsync readExcludedPlaylist
        |> Task.map (List.map (fun ep -> ExcludedPlaylist(Id = ep.Id, Name = ep.Name, Disabled = ep.Disabled)))

      printfn "Loaded %i excluded playlists for preset with id %i" (excludedPlaylists |> List.length) presetId

      return excludedPlaylists
    }

  let private loadTargetPlaylists connectionString presetId =
    task {
      let! targetPlaylists =
        connectionString
        |> Sql.connect
        |> Sql.query
          $"SELECT * FROM \"spotify-playlist-generator\".public.\"Playlists\" as P where P.\"PlaylistType\" = 2 and P.\"PresetId\" = {presetId}"
        |> Sql.executeAsync readTargetedPlaylist
        |> Task.map (List.map (fun tp -> TargetedPlaylist(Id = tp.Id, Name = tp.Name, Disabled = tp.Disabled, Overwrite = tp.Overwrite)))

      printfn "Loaded %i target playlists for preset with id %i" (targetPlaylists |> List.length) presetId

      return targetPlaylists
    }

  let loadPresets connectionString =
    let loadIncludedPlaylists = loadIncludedPlaylists connectionString
    let loadExcludedPlaylists = loadExcludedPlaylists connectionString
    let loadTargetPlaylists = loadTargetPlaylists connectionString

    fun userId ->
      task {
        let options = GenerationOptions(true, false, 12)

        let! presets =
          connectionString
          |> Sql.connect
          |> Sql.query $"SELECT * FROM \"spotify-playlist-generator\".public.\"Presets\" as P where P.\"UserId\" = {userId}"
          |> Sql.executeAsync readPreset
          |> Task.bind (
            List.map (fun p ->
              task {
                let! includedPlaylists = loadIncludedPlaylists p.Id
                let! excludedPlaylists = loadExcludedPlaylists p.Id
                let! targetPlaylists = loadTargetPlaylists p.Id

                return
                  Database.Entities.Preset(
                    Id = ShortId.Generate(options),
                    Name = p.Name,
                    UserId = p.UserId,
                    Settings =
                      Settings(
                        PlaylistSize = p.Settings.PlaylistSize,
                        IncludeLikedTracks = (p.Settings.IncludeLikedTracks |> Option.toNullable),
                        RecommendationsEnabled = p.Settings.RecommendationsEnabled
                      ),
                    IncludedPlaylists = includedPlaylists,
                    ExcludedPlaylists = excludedPlaylists,
                    TargetedPlaylists = targetPlaylists
                  )

              })
            >> Task.WhenAll
          )

        printfn "Loaded %i presets for user with id %i from PG DB" (presets |> Array.length) userId

        return presets
      }

  let loadUsers connectionString =
    fun () ->
      task {
        let! users =
          connectionString
          |> Sql.connect
          |> Sql.query "SELECT * FROM \"spotify-playlist-generator\".public.\"Users\""
          |> Sql.executeAsync readUser

        printfn "Loaded %i users from PG DB" (users |> List.length)

        return users
      }

module Mongo =
  let mapPreset (preset: Preset) =
    SimplePreset(Id = preset.Id, Name = preset.Name)

  let mapUser (user: {| Id: int64 |}) presets =
    Database.Entities.User(Id = user.Id, Presets = (presets |> Seq.map mapPreset))

  let savePresets (connectionString: string) =
    fun (user: {| Id: int64 |}, presets) ->
      task {
        let database =
          MongoClient(connectionString).GetDatabase "spotify-playlist-generator"

        let presetsCollection = database.GetCollection "presets"

        do! presetsCollection.InsertManyAsync(presets)

        printfn "Imported %i presets for user with id %i" (presets |> Array.length) user.Id

        let usersCollection = database.GetCollection "users"

        let user = mapUser user presets

        do! usersCollection.InsertOneAsync user

        printfn "Imported user with id %i" user.Id

        return ()
      }

let pgConnectionString =
  "<PostgreSQL connection string>"

let mongoConnectionString =
  "<MongoDB connection string>"

let loadUsers = PG.loadUsers pgConnectionString
let loadPresets = PG.loadPresets pgConnectionString
let savePresets = Mongo.savePresets mongoConnectionString

loadUsers ()
|> Task.bind (
  Seq.map (fun u ->
    task {
      let! presets = loadPresets u.Id

      return (u, presets)
    })
  >> Task.WhenAll
)
|> Task.bind (Seq.map savePresets >> Task.WhenAll)
|> Async.AwaitTask
|> Async.RunSynchronously
|> ignore

printfn "Migration from PG to Mongo done!"
