module Domain.Workflows

open System.Threading.Tasks
open Domain.Core
open Domain.Extensions
open IcedTasks.ColdTasks
open Microsoft.FSharp.Control
open shortid
open shortid.Configuration

[<RequireQualifiedAccess>]
module UserId =
  let value (UserId id) = id

[<RequireQualifiedAccess>]
module PresetId =
  let create () =
    let options = GenerationOptions(true, false, 12)

    ShortId.Generate(options) |> PresetId

  let value (PresetId id) = id

[<RequireQualifiedAccess>]
module ReadablePlaylistId =
  let value (ReadablePlaylistId id) = id

[<RequireQualifiedAccess>]
module PlaylistId =
  let value (PlaylistId id) = id

[<RequireQualifiedAccess>]
module WritablePlaylistId =
  let value (WritablePlaylistId id) = id

[<RequireQualifiedAccess>]
module User =
  type ListLikedTracks = ColdTask<TrackId list>
  type Load = UserId -> Task<User>
  type Update = User -> Task<unit>
  type Create = User -> Task<unit>
  type Exists = UserId -> Task<bool>

  let setCurrentPreset (load: Load) (update: Update) : User.SetCurrentPreset =
    fun userId presetId ->
      userId
      |> load
      |> Task.map (fun u ->
        { u with
            CurrentPresetId = Some presetId })
      |> Task.taskMap update

[<RequireQualifiedAccess>]
module SimplePreset =
  let fromPreset (preset: Preset) = { Id = preset.Id; Name = preset.Name }

[<RequireQualifiedAccess>]
module Preset =
  type Load = PresetId -> Task<Preset>
  type Save = Preset -> Task<unit>
  type Update = Preset -> Task<unit>
  type UpdateSettings = PresetId -> PresetSettings.PresetSettings -> Task<unit>
  type GetRecommendations = int -> TrackId list -> Task<TrackId list>
  type Remove = PresetId -> Task<Preset>

  type ListIncludedTracks = IncludedPlaylist list -> Task<TrackId list>
  type ListExcludedTracks = ExcludedPlaylist list -> Task<TrackId list>

  let validate: Preset.Validate =
    fun preset ->
      match preset.IncludedPlaylists, preset.Settings.LikedTracksHandling, preset.TargetedPlaylists with
      | [], PresetSettings.LikedTracksHandling.Include, [] ->
        [ Preset.ValidationError.NoTargetedPlaylists ] |> Preset.ValidationResult.Errors
      | [], PresetSettings.LikedTracksHandling.Exclude, [] ->
        [ Preset.ValidationError.NoIncludedPlaylists
          Preset.ValidationError.NoTargetedPlaylists ]
        |> Preset.ValidationResult.Errors
      | [], PresetSettings.LikedTracksHandling.Ignore, [] ->
        [ Preset.ValidationError.NoIncludedPlaylists
          Preset.ValidationError.NoTargetedPlaylists ]
        |> Preset.ValidationResult.Errors
      | _, PresetSettings.LikedTracksHandling.Include, [] ->
        [ Preset.ValidationError.NoTargetedPlaylists ] |> Preset.ValidationResult.Errors
      | _, PresetSettings.LikedTracksHandling.Exclude, [] ->
        [ Preset.ValidationError.NoTargetedPlaylists ] |> Preset.ValidationResult.Errors
      | _, PresetSettings.LikedTracksHandling.Ignore, [] -> [ Preset.ValidationError.NoTargetedPlaylists ] |> Preset.ValidationResult.Errors
      | [], PresetSettings.LikedTracksHandling.Exclude, _ ->
        [ Preset.ValidationError.NoIncludedPlaylists ] |> Preset.ValidationResult.Errors
      | [], PresetSettings.LikedTracksHandling.Ignore, _ -> [ Preset.ValidationError.NoIncludedPlaylists ] |> Preset.ValidationResult.Errors
      | _ -> Preset.ValidationResult.Ok

  let setLikedTracksHandling (load: Load) (update: Update) : Preset.SetLikedTracksHandling =
    fun presetId handling ->
      presetId
      |> load
      |> Task.map (fun p ->
        { p with
            Settings =
              { p.Settings with
                  LikedTracksHandling = handling } })
      |> Task.taskMap update

  let setPlaylistSize (load: Load) (update: Update) : Preset.SetPlaylistSize =
    fun presetId size ->
      presetId
      |> load
      |> Task.map (fun p ->
        { p with
            Settings = { p.Settings with PlaylistSize = size } })
      |> Task.taskMap update

  let create (savePreset: Save) (loadUser: User.Load) (updateUser: User.Update) userId : Preset.Create =
    fun name ->
      task {
        let newPreset =
          { Id = PresetId.create ()
            Name = name
            IncludedPlaylists = []
            ExcludedPlaylist = []
            TargetedPlaylists = []
            Settings =
              { PlaylistSize = (PresetSettings.PlaylistSize 20)
                RecommendationsEnabled = false
                LikedTracksHandling = PresetSettings.LikedTracksHandling.Include }
            UserId = userId }

        let! user = loadUser userId

        let userPreset = newPreset |> SimplePreset.fromPreset

        let updatedUser =
          { user with
              Presets = user.Presets |> List.append [ userPreset ] }

        do! updateUser updatedUser
        do! savePreset newPreset

        return newPreset.Id
      }

  let remove (loadUser: User.Load) (remove: Remove) (updateUser: User.Update) : Preset.Remove =
    fun presetId ->
      task {
        let! preset = remove presetId

        let! user = loadUser preset.UserId

        let userPreset = preset |> SimplePreset.fromPreset

        let updatedUser =
          { user with
              Presets = user.Presets |> List.except [ userPreset ] }

        do! updateUser updatedUser

        return preset
      }

  let enableRecommendations (loadPreset: Load) (update: Update) : Preset.EnableRecommendations =
    loadPreset
    >> Task.map (fun p ->
      { p with
          Settings =
            { p.Settings with
                RecommendationsEnabled = true } })
    >> Task.bind update

  let disableRecommendations (loadPreset: Load) (update: Update) : Preset.DisableRecommendations =
    loadPreset
    >> Task.map (fun p ->
      { p with
          Settings =
            { p.Settings with
                RecommendationsEnabled = false } })
    >> Task.bind update

[<RequireQualifiedAccess>]
module IncludedPlaylist =
  let enable (loadPreset: Preset.Load) (updatePreset: Preset.Update) : IncludedPlaylist.Enable =
    fun presetId playlistId ->
      task {
        let! preset = loadPreset presetId

        let playlist = preset.IncludedPlaylists |> List.find (fun p -> p.Id = playlistId)
        let updatedPlaylist = { playlist with Enabled = true }

        let updatedPreset =
          { preset with
              IncludedPlaylists =
                preset.IncludedPlaylists
                |> List.except [ playlist ]
                |> List.append [ updatedPlaylist ] }

        return! updatePreset updatedPreset
      }

  let disable (loadPreset: Preset.Load) (updatePreset: Preset.Update) : IncludedPlaylist.Disable =
    fun presetId playlistId ->
      task {
        let! preset = loadPreset presetId

        let playlist = preset.IncludedPlaylists |> List.find (fun p -> p.Id = playlistId)
        let updatedPlaylist = { playlist with Enabled = false }

        let updatedPreset =
          { preset with
              IncludedPlaylists =
                preset.IncludedPlaylists
                |> List.except [ playlist ]
                |> List.append [ updatedPlaylist ] }

        return! updatePreset updatedPreset
      }

[<RequireQualifiedAccess>]
module Playlist =
  type ListTracks = ReadablePlaylistId -> Task<TrackId list>

  type UpdateTracks = TargetedPlaylist -> TrackId list -> Task<unit>

  type ParsedPlaylistId = ParsedPlaylistId of string

  type ParseId = Playlist.RawPlaylistId -> Result<ParsedPlaylistId, Playlist.IdParsingError>

  type LoadFromSpotify = ParsedPlaylistId -> Task<Result<SpotifyPlaylist, Playlist.MissingFromSpotifyError>>

  type IncludeInStorage = IncludedPlaylist -> Async<IncludedPlaylist>
  type ExcludeInStorage = ExcludedPlaylist -> Async<ExcludedPlaylist>
  type TargetInStorage = TargetedPlaylist -> Async<TargetedPlaylist>

  type CountTracks = PlaylistId -> Task<int64>

  let includePlaylist
    (parseId: ParseId)
    (loadFromSpotify: LoadFromSpotify)
    (loadPreset: Preset.Load)
    (updatePreset: Preset.Update)
    : Playlist.IncludePlaylist =
    let parseId = parseId >> Result.mapError Playlist.IncludePlaylistError.IdParsing

    let loadFromSpotify =
      (loadFromSpotify >> Async.AwaitTask)
      >> AsyncResult.mapError Playlist.IncludePlaylistError.MissingFromSpotify

    fun presetId rawPlaylistId ->
      let updatePreset playlist =
        task {
          let! preset = loadPreset presetId |> Async.AwaitTask

          let updatedIncludedPlaylists = preset.IncludedPlaylists |> List.append [ playlist ]

          let updatedPreset =
            { preset with
                IncludedPlaylists = updatedIncludedPlaylists }

          do! updatePreset updatedPreset

          return playlist
        }

      rawPlaylistId
      |> parseId
      |> Result.asyncBind loadFromSpotify
      |> AsyncResult.map IncludedPlaylist.fromSpotifyPlaylist
      |> AsyncResult.asyncMap (updatePreset >> Async.AwaitTask)

  let excludePlaylist
    (parseId: ParseId)
    (loadFromSpotify: LoadFromSpotify)
    (loadPreset: Preset.Load)
    (updatePreset: Preset.Update)
    : Playlist.ExcludePlaylist =
    let parseId = parseId >> Result.mapError Playlist.ExcludePlaylistError.IdParsing

    let loadFromSpotify =
      (loadFromSpotify >> Async.AwaitTask)
      >> AsyncResult.mapError Playlist.ExcludePlaylistError.MissingFromSpotify

    fun presetId rawPlaylistId ->
      let updatePreset playlist =
        task {
          let! preset = loadPreset presetId |> Async.AwaitTask

          let updatedExcludedPlaylists = preset.ExcludedPlaylist |> List.append [ playlist ]

          let updatedPreset =
            { preset with
                ExcludedPlaylist = updatedExcludedPlaylists }

          do! updatePreset updatedPreset

          return playlist
        }

      rawPlaylistId
      |> parseId
      |> Result.asyncBind loadFromSpotify
      |> AsyncResult.map ExcludedPlaylist.fromSpotifyPlaylist
      |> AsyncResult.asyncMap (updatePreset >> Async.AwaitTask)

  let targetPlaylist
    (parseId: ParseId)
    (loadFromSpotify: LoadFromSpotify)
    (loadPreset: Preset.Load)
    (updatePreset: Preset.Update)
    : Playlist.TargetPlaylist =
    let parseId = parseId >> Result.mapError Playlist.TargetPlaylistError.IdParsing

    let loadFromSpotify =
      (loadFromSpotify >> Async.AwaitTask)
      >> AsyncResult.mapError Playlist.TargetPlaylistError.MissingFromSpotify

    let checkAccess playlist =
      playlist
      |> TargetedPlaylist.fromSpotifyPlaylist
      |> Result.ofOption (Playlist.AccessError() |> Playlist.TargetPlaylistError.AccessError)

    fun presetId rawPlaylistId ->
      let updatePreset playlist =
        task {
          let! preset = loadPreset presetId |> Async.AwaitTask

          let updatedTargetedPlaylists = preset.TargetedPlaylists |> List.append [ playlist ]

          let updatedPreset =
            { preset with
                TargetedPlaylists = updatedTargetedPlaylists }

          do! updatePreset updatedPreset

          return playlist
        }

      rawPlaylistId
      |> parseId
      |> Result.asyncBind loadFromSpotify
      |> AsyncResult.bindSync checkAccess
      |> AsyncResult.asyncMap (updatePreset >> Async.AwaitTask)

  type GenerateIO ={
    LogPotentialTracks: int -> unit
    ListIncludedTracks: Preset.ListIncludedTracks
    ListExcludedTracks: Preset.ListExcludedTracks
    ListLikedTracks: User.ListLikedTracks
    LoadPreset: Preset.Load
    UpdateTargetedPlaylists: UpdateTracks
    GetRecommendations: Preset.GetRecommendations
  }

  let generate (io: GenerateIO) : Playlist.Generate =
    fun presetId ->
      task {
        let! preset = io.LoadPreset presetId

        let! likedTracks = io.ListLikedTracks

        let! includedTracks =
          preset.IncludedPlaylists
          |> io.ListIncludedTracks
          |> Task.map List.shuffle

        let! excludedTracks =
          preset.ExcludedPlaylist
          |> io.ListExcludedTracks

        let excludedTracks, includedTracks =
          match preset.Settings.LikedTracksHandling with
          | PresetSettings.LikedTracksHandling.Include -> excludedTracks, includedTracks @ likedTracks
          | PresetSettings.LikedTracksHandling.Exclude -> likedTracks @ excludedTracks, includedTracks
          | PresetSettings.LikedTracksHandling.Ignore -> excludedTracks, includedTracks

        let! recommendedTracks =
          if preset.Settings.RecommendationsEnabled then
            includedTracks |> List.take 5 |> (io.GetRecommendations 100)
          else
            [] |> Task.FromResult

        let includedTracksIds = includedTracks @ recommendedTracks

        let potentialTracksIds = includedTracksIds |> List.except excludedTracks

        io.LogPotentialTracks potentialTracksIds.Length

        return!
          match potentialTracksIds with
          | [] -> Playlist.GenerateError.NoPotentialTracks |> Error |> Task.FromResult
          | tracks ->
            task{
              let tracksIdsToImport =
                tracks
                |> List.take (preset.Settings.PlaylistSize |> PlaylistSize.value)

              for playlist in preset.TargetedPlaylists |> Seq.filter (fun p -> p.Enabled) do
                do! io.UpdateTargetedPlaylists playlist tracksIdsToImport

              return Ok()
            }
      }

[<RequireQualifiedAccess>]
module TargetedPlaylist =
  type Update = PresetId -> TargetedPlaylist -> Task<unit>
  type Remove = PresetId -> TargetedPlaylistId -> Task<unit>

  let overwriteTargetedPlaylist (loadPreset: Preset.Load) (updatePreset: Preset.Update) : TargetedPlaylist.OverwriteTracks =
    fun presetId targetedPlaylistId ->
      task {
        let! preset = loadPreset presetId

        let targetPlaylist =
          preset.TargetedPlaylists |> List.find (fun p -> p.Id = targetedPlaylistId)

        let updatedPlaylist = { targetPlaylist with Overwrite = true }

        let updatedPreset =
          { preset with
              TargetedPlaylists =
                preset.TargetedPlaylists
                |> List.except [ targetPlaylist ]
                |> List.append [ updatedPlaylist ] }

        return! updatePreset updatedPreset
      }

  let appendToTargetedPlaylist (loadPreset: Preset.Load) (updatePreset: Preset.Update) : TargetedPlaylist.AppendTracks =
    fun presetId targetPlaylistId ->
      task {
        let! preset = loadPreset presetId

        let targetPlaylist =
          preset.TargetedPlaylists |> List.find (fun p -> p.Id = targetPlaylistId)

        let updatedPlaylist =
          { targetPlaylist with
              Overwrite = false }

        let updatedPreset =
          { preset with
              TargetedPlaylists =
                preset.TargetedPlaylists
                |> List.except [ targetPlaylist ]
                |> List.append [ updatedPlaylist ] }

        return! updatePreset updatedPreset
      }

  let overwriteTracks (loadPreset: Preset.Load) (updatePreset: Preset.Update) : TargetedPlaylist.OverwriteTracks =
    fun presetId targetPlaylistId ->
      task {
        let! preset = loadPreset presetId

        let targetPlaylist =
          preset.TargetedPlaylists |> List.find (fun p -> p.Id = targetPlaylistId)

        let updatedPlaylist = { targetPlaylist with Overwrite = true }

        let updatedPreset =
          { preset with
              TargetedPlaylists =
                preset.TargetedPlaylists
                |> List.except [ targetPlaylist ]
                |> List.append [ updatedPlaylist ] }

        return! updatePreset updatedPreset
      }

  let appendTracks (loadPreset: Preset.Load) (updatePreset: Preset.Update) : TargetedPlaylist.AppendTracks =
    fun presetId targetPlaylistId ->
      task {
        let! preset = loadPreset presetId

        let targetPlaylist =
          preset.TargetedPlaylists |> List.find (fun p -> p.Id = targetPlaylistId)

        let updatedPlaylist =
          { targetPlaylist with
              Overwrite = false }

        let updatedPreset =
          { preset with
              TargetedPlaylists =
                preset.TargetedPlaylists
                |> List.filter ((<>) targetPlaylist)
                |> List.append [ updatedPlaylist ] }

        return! updatePreset updatedPreset
      }

  let remove (loadPreset: Preset.Load) (updatePreset: Preset.Update) : TargetedPlaylist.Remove =
    fun presetId targetPlaylistId ->
      task {
        let! preset = loadPreset presetId

        let targetPlaylists =
          preset.TargetedPlaylists |> List.filter (fun p -> p.Id <> targetPlaylistId)

        let updatedPreset =
          { preset with
              TargetedPlaylists = targetPlaylists }

        return! updatePreset updatedPreset
      }
