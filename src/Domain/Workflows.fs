module Domain.Workflows

open System.Threading.Tasks
open Domain.Core
open Domain.Extensions
open IcedTasks.ColdTasks
open Microsoft.FSharp.Control
open Microsoft.FSharp.Core
open otsom.fs.Extensions
open otsom.fs.Telegram.Bot.Core
open shortid
open shortid.Configuration

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
  type CreateIfNotExists = UserId -> Task<unit>
  type Exists = UserId -> Task<bool>

  let setCurrentPreset (load: Load) (update: Update) : User.SetCurrentPreset =
    fun userId presetId ->
      userId
      |> load
      |> Task.map (fun u ->
        { u with
            CurrentPresetId = Some presetId })
      |> Task.bind update

[<RequireQualifiedAccess>]
module SimplePreset =
  let fromPreset (preset: Preset) = { Id = preset.Id; Name = preset.Name }

[<RequireQualifiedAccess>]
module Preset =
  type Load = PresetId -> Task<Preset>
  type Save = Preset -> Task<unit>
  type Update = Preset -> Task<unit>
  type UpdateSettings = PresetId -> PresetSettings.PresetSettings -> Task<unit>
  type GetRecommendations = TrackId list -> Task<TrackId list>
  type Remove = PresetId -> Task<Preset>

  type ListIncludedTracks = IncludedPlaylist list -> Task<TrackId list>
  type ListExcludedTracks = ExcludedPlaylist list -> Task<TrackId list>

  let validate: Preset.Validate =
    fun preset ->
      match preset.IncludedPlaylists, preset.Settings.LikedTracksHandling, preset.TargetedPlaylists with
      | [], PresetSettings.LikedTracksHandling.Include, [] ->
        [ Preset.ValidationError.NoTargetedPlaylists ] |> Error
      | [], PresetSettings.LikedTracksHandling.Exclude, [] ->
        [ Preset.ValidationError.NoIncludedPlaylists
          Preset.ValidationError.NoTargetedPlaylists ]
        |> Error
      | [], PresetSettings.LikedTracksHandling.Ignore, [] ->
        [ Preset.ValidationError.NoIncludedPlaylists
          Preset.ValidationError.NoTargetedPlaylists ]
        |> Error
      | _, PresetSettings.LikedTracksHandling.Include, [] ->
        [ Preset.ValidationError.NoTargetedPlaylists ] |> Error
      | _, PresetSettings.LikedTracksHandling.Exclude, [] ->
        [ Preset.ValidationError.NoTargetedPlaylists ] |> Error
      | _, PresetSettings.LikedTracksHandling.Ignore, [] -> [ Preset.ValidationError.NoTargetedPlaylists ] |> Error
      | [], PresetSettings.LikedTracksHandling.Exclude, _ ->
        [ Preset.ValidationError.NoIncludedPlaylists ] |> Error
      | [], PresetSettings.LikedTracksHandling.Ignore, _ -> [ Preset.ValidationError.NoIncludedPlaylists ] |> Error
      | _ -> Ok preset

  let private setLikedTracksHandling (load: Load) (update: Update) =
    fun handling presetId ->
      presetId
      |> load
      |> Task.map (fun p ->
        { p with
            Settings =
              { p.Settings with
                  LikedTracksHandling = handling } })
      |> Task.bind update

  let includeLikedTracks load update : Preset.IncludeLikedTracks =
    setLikedTracksHandling load update PresetSettings.LikedTracksHandling.Include

  let excludeLikedTracks load update : Preset.ExcludeLikedTracks =
    setLikedTracksHandling load update PresetSettings.LikedTracksHandling.Exclude

  let ignoreLikedTracks load update : Preset.IgnoreLikedTracks =
    setLikedTracksHandling load update PresetSettings.LikedTracksHandling.Ignore

  let setPlaylistSize (load: Load) (update: Update) : Preset.SetPlaylistSize =
    fun presetId size ->
      presetId
      |> load
      |> Task.map (fun p ->
        { p with
            Settings = { p.Settings with PlaylistSize = size } })
      |> Task.bind update

  let create (savePreset: Save) (loadUser: User.Load) (updateUser: User.Update) userId : Preset.Create =
    fun name ->
      task {
        let newPreset =
          { Id = PresetId.create ()
            Name = name
            IncludedPlaylists = []
            ExcludedPlaylists = []
            TargetedPlaylists = []
            Settings =
              { PlaylistSize = (PresetSettings.PlaylistSize.create 20)
                RecommendationsEnabled = false
                LikedTracksHandling = PresetSettings.LikedTracksHandling.Include }
            UserId = userId }

        let! user = loadUser userId

        let userPreset = newPreset |> SimplePreset.fromPreset

        let updatedUser =
          { user with
              Presets = user.Presets @ [ userPreset ] }

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
              Presets = user.Presets |> List.except [ userPreset ]
              CurrentPresetId = user.CurrentPresetId |> Option.filter (fun currentId -> currentId <> presetId) }

        do! updateUser updatedUser

        return preset
      }

  let private setRecommendations (load: Load) (update: Update) =
    fun enabled ->
      load
      >> Task.map (fun preset ->
        { preset with
            Settings =
              { preset.Settings with
                  RecommendationsEnabled = enabled } })
      >> Task.bind update

  let enableRecommendations load update : Preset.EnableRecommendations = setRecommendations load update true

  let disableRecommendations load update : Preset.DisableRecommendations = setRecommendations load update false

[<RequireQualifiedAccess>]
module IncludedPlaylist =
  let private updatePresetPlaylist (loadPreset: Preset.Load) (updatePreset: Preset.Update) enable =
    fun presetId playlistId ->
      task {
        let! preset = loadPreset presetId

        let playlist = preset.IncludedPlaylists |> List.find (fun p -> p.Id = playlistId)
        let updatedPlaylist = { playlist with Enabled = enable }

        let updatedPreset =
          { preset with
              IncludedPlaylists =
                preset.IncludedPlaylists
                |> List.except [ playlist ]
                |> List.append [ updatedPlaylist ] }

        return! updatePreset updatedPreset
      }

  let enable loadPreset updatePreset : IncludedPlaylist.Enable =
    updatePresetPlaylist loadPreset updatePreset true

  let disable loadPreset updatePreset : IncludedPlaylist.Disable =
    updatePresetPlaylist loadPreset updatePreset false

  let remove (loadPreset: Preset.Load) (updatePreset: Preset.Update) : IncludedPlaylist.Remove =
    fun presetId includedPlaylistId ->
      task {
        let! preset = loadPreset presetId

        let includedPlaylists =
          preset.IncludedPlaylists |> List.filter (fun p -> p.Id <> includedPlaylistId)

        let updatedPreset =
          { preset with
              IncludedPlaylists = includedPlaylists }

        return! updatePreset updatedPreset
      }

[<RequireQualifiedAccess>]
module ExcludedPlaylist =
  let private updatePresetPlaylist (loadPreset: Preset.Load) (updatePreset: Preset.Update) enable =
    fun presetId playlistId ->
      task {
        let! preset = loadPreset presetId

        let playlist = preset.ExcludedPlaylists |> List.find (fun p -> p.Id = playlistId)
        let updatedPlaylist = { playlist with Enabled = enable }

        let updatedPreset =
          { preset with
              ExcludedPlaylists =
                preset.ExcludedPlaylists
                |> List.except [ playlist ]
                |> List.append [ updatedPlaylist ] }

        return! updatePreset updatedPreset
      }

  let enable loadPreset updatePreset : ExcludedPlaylist.Enable =
    updatePresetPlaylist loadPreset updatePreset true

  let disable loadPreset updatePreset : ExcludedPlaylist.Disable =
    updatePresetPlaylist loadPreset updatePreset false

  let remove (loadPreset: Preset.Load) (updatePreset: Preset.Update) : ExcludedPlaylist.Remove =
    fun presetId excludedPlaylistId ->
      task {
        let! preset = loadPreset presetId

        let excludedPlaylists =
          preset.ExcludedPlaylists |> List.filter (fun p -> p.Id <> excludedPlaylistId)

        let updatedPreset =
          { preset with
              ExcludedPlaylists = excludedPlaylists }

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
      loadFromSpotify
      >> TaskResult.mapError Playlist.IncludePlaylistError.MissingFromSpotify

    fun presetId rawPlaylistId ->
      let updatePreset playlist =
        task {
          let! preset = loadPreset presetId

          let updatedIncludedPlaylists = preset.IncludedPlaylists |> List.append [ playlist ]

          let updatedPreset =
            { preset with
                IncludedPlaylists = updatedIncludedPlaylists }

          do! updatePreset updatedPreset

          return playlist
        }

      rawPlaylistId
      |> parseId
      |> Result.taskBind loadFromSpotify
      |> TaskResult.map IncludedPlaylist.fromSpotifyPlaylist
      |> TaskResult.taskMap updatePreset

  let excludePlaylist
    (parseId: ParseId)
    (loadFromSpotify: LoadFromSpotify)
    (loadPreset: Preset.Load)
    (updatePreset: Preset.Update)
    : Playlist.ExcludePlaylist =
    let parseId = parseId >> Result.mapError Playlist.ExcludePlaylistError.IdParsing

    let loadFromSpotify =
      loadFromSpotify
      >> TaskResult.mapError Playlist.ExcludePlaylistError.MissingFromSpotify

    fun presetId rawPlaylistId ->
      let updatePreset playlist =
        task {
          let! preset = loadPreset presetId

          let updatedExcludedPlaylists = preset.ExcludedPlaylists |> List.append [ playlist ]

          let updatedPreset =
            { preset with
                ExcludedPlaylists = updatedExcludedPlaylists }

          do! updatePreset updatedPreset

          return playlist
        }

      rawPlaylistId
      |> parseId
      |> Result.taskBind loadFromSpotify
      |> TaskResult.map ExcludedPlaylist.fromSpotifyPlaylist
      |> TaskResult.taskMap updatePreset

  let targetPlaylist
    (parseId: ParseId)
    (loadFromSpotify: LoadFromSpotify)
    (loadPreset: Preset.Load)
    (updatePreset: Preset.Update)
    : Playlist.TargetPlaylist =
    let parseId = parseId >> Result.mapError Playlist.TargetPlaylistError.IdParsing

    let loadFromSpotify =
      loadFromSpotify
      >> TaskResult.mapError Playlist.TargetPlaylistError.MissingFromSpotify

    let checkAccess playlist =
      playlist
      |> TargetedPlaylist.fromSpotifyPlaylist
      |> Result.ofOption (Playlist.AccessError() |> Playlist.TargetPlaylistError.AccessError)

    fun presetId rawPlaylistId ->
      let updatePreset playlist =
        task {
          let! preset = loadPreset presetId

          let updatedTargetedPlaylists = preset.TargetedPlaylists |> List.append [ playlist ]

          let updatedPreset =
            { preset with
                TargetedPlaylists = updatedTargetedPlaylists }

          do! updatePreset updatedPreset

          return playlist
        }

      rawPlaylistId
      |> parseId
      |> Result.taskBind loadFromSpotify
      |> TaskResult.bind checkAccess
      |> TaskResult.taskMap updatePreset

  type GenerateIO =
    { LogPotentialTracks: int -> unit
      ListIncludedTracks: Preset.ListIncludedTracks
      ListExcludedTracks: Preset.ListExcludedTracks
      ListLikedTracks: User.ListLikedTracks
      LoadPreset: Preset.Load
      UpdateTargetedPlaylists: UpdateTracks
      GetRecommendations: Preset.GetRecommendations }

  let generate (io: GenerateIO) : Playlist.Generate =

    let saveTracks preset =
      fun tracks ->
        match tracks with
        | [] -> Playlist.GenerateError.NoPotentialTracks |> Error |> Task.FromResult
        | tracks ->
          task {
            let tracksIdsToImport =
              tracks |> List.takeSafe (preset.Settings.PlaylistSize |> PresetSettings.PlaylistSize.value)

            for playlist in preset.TargetedPlaylists |> Seq.filter _.Enabled do
              do! io.UpdateTargetedPlaylists playlist tracksIdsToImport

            return Ok()
          }

    let generateAndSaveTracks preset =
      fun includedTracks excludedTracks ->
        match includedTracks with
        | [] -> Playlist.GenerateError.NoIncludedTracks |> Error |> Task.FromResult
        | includedTracks ->
          task {
            let! recommendedTracks =
              if preset.Settings.RecommendationsEnabled then
                includedTracks |> io.GetRecommendations
              else
                [] |> Task.FromResult

            let includedTracks = recommendedTracks @ includedTracks

            let potentialTracks = includedTracks |> List.except excludedTracks

            io.LogPotentialTracks potentialTracks.Length

            return! saveTracks preset potentialTracks
          }

    fun presetId ->
      task {
        let! preset = io.LoadPreset presetId

        let! likedTracks = io.ListLikedTracks

        let! includedTracks = preset.IncludedPlaylists |> io.ListIncludedTracks

        let! excludedTracks = preset.ExcludedPlaylists |> io.ListExcludedTracks

        let excludedTracks, includedTracks =
          match preset.Settings.LikedTracksHandling with
          | PresetSettings.LikedTracksHandling.Include -> excludedTracks, includedTracks @ likedTracks
          | PresetSettings.LikedTracksHandling.Exclude -> likedTracks @ excludedTracks, includedTracks
          | PresetSettings.LikedTracksHandling.Ignore -> excludedTracks, includedTracks

        return! generateAndSaveTracks preset (includedTracks |> List.shuffle) excludedTracks
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