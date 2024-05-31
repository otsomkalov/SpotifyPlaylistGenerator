module Domain.Workflows

open System.Threading.Tasks
open Domain.Core
open Domain.Extensions
open Domain.Repos
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
module Tracks =
  let uniqueByArtists (tracks: Track seq) =
    let addUniqueTrack (knownArtists, uniqueTracks) currentTrack =
      if
        knownArtists |> Set.intersect currentTrack.Artists |> Set.isEmpty
      then
        (knownArtists |> Set.union currentTrack.Artists, currentTrack :: uniqueTracks)
      else
        knownArtists, uniqueTracks

    tracks |> Seq.fold addUniqueTrack (Set.empty, []) |> snd

[<RequireQualifiedAccess>]
module User =
  type ListLikedTracks = ColdTask<Track list>
  type Update = User -> Task<unit>
  type CreateIfNotExists = UserId -> Task<unit>
  type Exists = UserId -> Task<bool>

  let get (load: UserRepo.Load) : User.Get = load

  let setCurrentPreset (load: UserRepo.Load) (update: Update) : User.SetCurrentPreset =
    fun userId presetId ->
      userId
      |> load
      |> Task.map (fun u ->
        { u with
            CurrentPresetId = Some presetId })
      |> Task.bind update

  let removePreset (load: UserRepo.Load) (removePreset: Preset.Remove) (update: Update) : User.RemovePreset =
    fun userId presetId ->
      userId
      |> load
      |> Task.map (fun u ->
        { u with
            Presets = u.Presets |> List.filter (fun p -> p.Id <> presetId)
            CurrentPresetId = if u.CurrentPresetId = Some presetId then None else u.CurrentPresetId })
      |> Task.bind update
      |> Task.bind (fun _ -> removePreset presetId)

[<RequireQualifiedAccess>]
module SimplePreset =
  let fromPreset (preset: Preset) = { Id = preset.Id; Name = preset.Name }

[<RequireQualifiedAccess>]
module Preset =
  type Save = Preset -> Task<unit>
  type UpdateSettings = PresetId -> PresetSettings.PresetSettings -> Task<unit>
  type GetRecommendations = TrackId list -> Task<Track list>

  let get (load: PresetRepo.Load) : Preset.Get =
    load

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

  let private setLikedTracksHandling (load: PresetRepo.Load) (update: PresetRepo.Update) =
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

  let setPlaylistSize (load: PresetRepo.Load) (update: PresetRepo.Update) : Preset.SetPlaylistSize =
    fun presetId size ->
      presetId
      |> load
      |> Task.map (fun p ->
        { p with
            Settings = { p.Settings with PlaylistSize = size } })
      |> Task.bind update

  let create (savePreset: Save) (loadUser: UserRepo.Load) (updateUser: User.Update) userId : Preset.Create =
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
                LikedTracksHandling = PresetSettings.LikedTracksHandling.Include
                UniqueArtists = false } }

        let! user = loadUser userId

        let userPreset = newPreset |> SimplePreset.fromPreset

        let updatedUser =
          { user with
              Presets = user.Presets @ [ userPreset ] }

        do! updateUser updatedUser
        do! savePreset newPreset

        return newPreset.Id
      }

  let remove (removePreset: Preset.Remove) : Preset.Remove =
    fun presetId ->
      removePreset presetId

  let private setRecommendations (load: PresetRepo.Load) (update: PresetRepo.Update) =
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

  let private setUniqueArtists (load: PresetRepo.Load) (update: PresetRepo.Update) =
    fun uniqueArists ->
      load
      >> Task.map (fun preset ->
        { preset with
            Settings =
              { preset.Settings with
                  UniqueArtists = uniqueArists } })
      >> Task.bind update

  let enableUniqueArtists load update : Preset.EnableUniqueArtists = setUniqueArtists load update true

  let disableUniqueArtists load update : Preset.DisableUniqueArtists = setUniqueArtists load update false

[<RequireQualifiedAccess>]
module IncludedPlaylist =
  let private updatePresetPlaylist (loadPreset: PresetRepo.Load) (updatePreset: PresetRepo.Update) enable =
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

  let remove (loadPreset: PresetRepo.Load) (updatePreset: PresetRepo.Update) : IncludedPlaylist.Remove =
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
  let private updatePresetPlaylist (loadPreset: PresetRepo.Load) (updatePreset: PresetRepo.Update) enable =
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

  let remove (loadPreset: PresetRepo.Load) (updatePreset: PresetRepo.Update) : ExcludedPlaylist.Remove =
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
  type ListTracks = ReadablePlaylistId -> Task<Track list>

  type UpdateTracks = TargetedPlaylist -> Track list -> Task<unit>

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
    (loadPreset: PresetRepo.Load)
    (updatePreset: PresetRepo.Update)
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
    (loadPreset: PresetRepo.Load)
    (updatePreset: PresetRepo.Update)
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
    (loadPreset: PresetRepo.Load)
    (updatePreset: PresetRepo.Update)
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
      |> Task.map (Result.bind checkAccess)
      |> TaskResult.taskMap updatePreset

  type GenerateIO =
    { ListIncludedTracks: PresetRepo.ListIncludedTracks
      ListExcludedTracks: PresetRepo.ListExcludedTracks
      ListLikedTracks: User.ListLikedTracks
      LoadPreset: PresetRepo.Load
      UpdateTargetedPlaylists: UpdateTracks
      GetRecommendations: Preset.GetRecommendations }

  let generate (io: GenerateIO) : Playlist.Generate =

    let saveTracks preset =
      fun (tracks: Track list) ->
        match tracks with
        | [] -> Playlist.GenerateError.NoPotentialTracks |> Error |> Task.FromResult
        | tracks ->
          task {
            let tracks =
              if preset.Settings.UniqueArtists then tracks |> Tracks.uniqueByArtists else tracks

            let tracksToImport =
              tracks |> List.takeSafe (preset.Settings.PlaylistSize |> PresetSettings.PlaylistSize.value)

            for playlist in preset.TargetedPlaylists |> Seq.filter _.Enabled do
              do! io.UpdateTargetedPlaylists playlist tracksToImport

            return Ok()
          }

    let includeLiked (preset: Preset) =
      fun tracks ->
        match preset.Settings.LikedTracksHandling with
        | PresetSettings.LikedTracksHandling.Include ->
          io.ListLikedTracks() |> Task.map (fun liked -> liked @ tracks)
        | _ ->
          Task.FromResult tracks

    let excludeLiked (preset: Preset) =
      fun tracks ->
        match preset.Settings.LikedTracksHandling with
        | PresetSettings.LikedTracksHandling.Exclude ->
          io.ListLikedTracks() |> Task.map (List.append tracks)
        | _ ->
          Task.FromResult tracks

    let getRecommendations (preset: Preset) =
      fun (tracks: Track list) ->
        match (tracks, preset.Settings.RecommendationsEnabled) with
        | [], false -> Playlist.GenerateError.NoIncludedTracks |> Error |> Task.FromResult
        | tracks, true -> io.GetRecommendations (tracks |> List.map (_.Id)) |> Task.map (fun recs -> recs @ tracks) |> Task.map Ok
        | _ -> [] |> Ok |> Task.FromResult

    let filterUniqueArtists (preset: Preset) =
      fun (tracks: Track list) ->
        match preset.Settings.UniqueArtists with
        | true -> tracks |> Tracks.uniqueByArtists
        | false -> tracks

    io.LoadPreset
    >> Task.bind (fun preset ->
      preset.IncludedPlaylists
      |> io.ListIncludedTracks
      |> Task.bind (includeLiked preset)
      |> Task.map List.shuffle
      |> Task.bind (getRecommendations preset)
      |> TaskResult.taskMap (fun includedTracks ->
        preset.ExcludedPlaylists
        |> io.ListExcludedTracks
        |> Task.bind (excludeLiked preset)
        |> Task.map (List.except includedTracks))
      |> TaskResult.map (filterUniqueArtists preset)
      |> TaskResult.map (List.takeSafe (preset.Settings.PlaylistSize |> PresetSettings.PlaylistSize.value))
      |> TaskResult.bind (saveTracks preset))

[<RequireQualifiedAccess>]
module TargetedPlaylist =
  let private setPlaylistOverwriting (loadPreset: PresetRepo.Load) (updatePreset: PresetRepo.Update) overwriting =
    fun presetId targetedPlaylistId ->
      task {
        let! preset = loadPreset presetId

        let targetPlaylist =
          preset.TargetedPlaylists |> List.find (fun p -> p.Id = targetedPlaylistId)

        let updatedPlaylist =
          { targetPlaylist with
              Overwrite = overwriting }

        let updatedPreset =
          { preset with
              TargetedPlaylists =
                preset.TargetedPlaylists
                |> List.except [ targetPlaylist ]
                |> List.append [ updatedPlaylist ] }

        return! updatePreset updatedPreset
      }

  let overwriteTracks (loadPreset: PresetRepo.Load) (updatePreset: PresetRepo.Update) : TargetedPlaylist.OverwriteTracks =
    setPlaylistOverwriting loadPreset updatePreset true

  let appendTracks (loadPreset: PresetRepo.Load) (updatePreset: PresetRepo.Update) : TargetedPlaylist.AppendTracks =
    setPlaylistOverwriting loadPreset updatePreset false

  let remove (loadPreset: PresetRepo.Load) (updatePreset: PresetRepo.Update) : TargetedPlaylist.Remove =
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
