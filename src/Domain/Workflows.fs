module Domain.Workflows

open System.Threading.Tasks
open Domain.Core
open Domain.Repos
open Microsoft.FSharp.Control
open Microsoft.FSharp.Core
open MusicPlatform
open otsom.fs.Extensions
open shortid
open shortid.Configuration
open Domain.Extensions

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

    tracks |> Seq.fold addUniqueTrack (Set.empty, []) |> snd |> List.rev

[<RequireQualifiedAccess>]
module SimplePreset =
  let fromPreset (preset: Preset) = { Id = preset.Id; Name = preset.Name }

[<RequireQualifiedAccess>]
module PresetSettings =
  let private setUniqueArtists (load: PresetRepo.Load) (update: PresetRepo.Save) =
    fun uniqueArtists ->
      load
      >> Task.map (fun preset ->
        { preset with
            Settings =
              { preset.Settings with
                  UniqueArtists = uniqueArtists } })
      >> Task.bind update

  let enableUniqueArtists load update : PresetSettings.EnableUniqueArtists = setUniqueArtists load update true

  let disableUniqueArtists load update : PresetSettings.DisableUniqueArtists = setUniqueArtists load update false

  let private setRecommendations (load: PresetRepo.Load) (update: PresetRepo.Save) =
    fun enabled ->
      load
      >> Task.map (fun preset ->
        { preset with
            Settings =
              { preset.Settings with
                  RecommendationsEnabled = enabled } })
      >> Task.bind update

  let enableRecommendations load update : PresetSettings.EnableRecommendations = setRecommendations load update true

  let disableRecommendations load update : PresetSettings.DisableRecommendations = setRecommendations load update false

  let private setLikedTracksHandling (load: PresetRepo.Load) (update: PresetRepo.Save) =
    fun handling presetId ->
      presetId
      |> load
      |> Task.map (fun p ->
        { p with
            Settings =
              { p.Settings with
                  LikedTracksHandling = handling } })
      |> Task.bind update

  let includeLikedTracks load update : PresetSettings.IncludeLikedTracks =
    setLikedTracksHandling load update PresetSettings.LikedTracksHandling.Include

  let excludeLikedTracks load update : PresetSettings.ExcludeLikedTracks =
    setLikedTracksHandling load update PresetSettings.LikedTracksHandling.Exclude

  let ignoreLikedTracks load update : PresetSettings.IgnoreLikedTracks =
    setLikedTracksHandling load update PresetSettings.LikedTracksHandling.Ignore

  let setPresetSize (load: PresetRepo.Load) (update: PresetRepo.Save) : PresetSettings.SetPresetSize =
    fun presetId size ->
      size
      |> PresetSettings.Size.tryParse
      |> Result.taskMap (fun s ->
        presetId
        |> load
        |> Task.map (fun p ->
          { p with
              Settings = { p.Settings with Size = s } })
        |> Task.bind update)

[<RequireQualifiedAccess>]
module IncludedPlaylist =
  let private listPlaylistTracks (env: #IListPlaylistTracks & #IListLikedTracks) =
    fun (playlist: IncludedPlaylist) -> task {
      let! tracks =
        playlist.Id
        |> ReadablePlaylistId.value
        |> env.ListPlaylistTracks
        |> Task.map Set.ofSeq

      if playlist.LikedOnly then
        let v = env.ListLikedTracks() |> Task.map (Set.ofList >> Set.intersect tracks)

        return! v
      else
        return tracks
    }

  let internal listTracks env =
    fun (playlists: IncludedPlaylist list) ->
      playlists
      |> List.filter _.Enabled
      |> List.map (listPlaylistTracks env)
      |> Task.WhenAll
      |> Task.map Seq.concat
      |> Task.map List.ofSeq

  let private updatePresetPlaylist (loadPreset: PresetRepo.Load) (updatePreset: PresetRepo.Save) enable =
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

  let remove (loadPreset: PresetRepo.Load) (updatePreset: PresetRepo.Save) : IncludedPlaylist.Remove =
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
module Preset =
  type UpdateSettings = PresetId -> PresetSettings.PresetSettings -> Task<unit>

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

  let create (savePreset: PresetRepo.Save) : Preset.Create =
    fun name ->
      task {
        let newPreset =
          { Id = PresetId.create ()
            Name = name
            IncludedPlaylists = []
            ExcludedPlaylists = []
            TargetedPlaylists = []
            Settings =
              { Size = (PresetSettings.Size.create 20)
                RecommendationsEnabled = false
                LikedTracksHandling = PresetSettings.LikedTracksHandling.Include
                UniqueArtists = false } }

        do! savePreset newPreset

        return newPreset
      }

  let remove (removePreset: Preset.Remove) : Preset.Remove =
    fun presetId ->
      removePreset presetId

  type RunIO =
    { ListExcludedTracks: PresetRepo.ListExcludedTracks
      LoadPreset: PresetRepo.Load
      AppendTracks: Playlist.AddTracks
      ReplaceTracks: TargetedPlaylistRepo.ReplaceTracks
      GetRecommendations: TrackRepo.GetRecommendations
      Shuffler: Track list -> Track list }

  let run (env: #IListLikedTracks) (io: RunIO) : Preset.Run =

    let saveTracks preset =
      fun (tracks: Track list) ->
        preset.TargetedPlaylists
        |> Seq.filter _.Enabled
        |> Seq.map (fun p ->
          match p.Overwrite with
          | true -> io.ReplaceTracks p.Id tracks
          | false -> io.AppendTracks (p.Id |> WritablePlaylistId.value) tracks)
        |> Task.WhenAll
        |> Task.ignore

    let includeLiked (preset: Preset) =
      fun tracks ->
        match preset.Settings.LikedTracksHandling with
        | PresetSettings.LikedTracksHandling.Include -> env.ListLikedTracks() |> Task.map (List.append tracks)
        | _ -> Task.FromResult tracks

    let excludeLiked (preset: Preset) =
      fun tracks ->
        match preset.Settings.LikedTracksHandling with
        | PresetSettings.LikedTracksHandling.Exclude -> env.ListLikedTracks() |> Task.map (List.append tracks)
        | _ -> Task.FromResult tracks

    let getRecommendations (preset: Preset) =
      fun (tracks: Track list) ->
        match preset.Settings.RecommendationsEnabled with
        | true ->
          tracks
          |> List.map (_.Id)
          |> io.GetRecommendations
          |> Task.map (List.prepend tracks)
        | false -> tracks |> Task.FromResult

    io.LoadPreset
    >> Task.bind (fun preset ->
      IncludedPlaylist.listTracks env preset.IncludedPlaylists
      &|&> includeLiked preset
      &|> Result.errorIf List.isEmpty Preset.RunError.NoIncludedTracks
      &=|> io.Shuffler
      &=|&> getRecommendations preset
      &=|&> (fun includedTracks ->
          preset.ExcludedPlaylists
          |> io.ListExcludedTracks
          &|&> (excludeLiked preset)
          &|> (fun excludedTracks -> List.except excludedTracks includedTracks)
      )
      &|> (Result.bind (Result.errorIf List.isEmpty Preset.RunError.NoPotentialTracks))
      &=|> (fun (tracks: Track list) ->
        match preset.Settings.UniqueArtists with
        | true -> tracks |> Tracks.uniqueByArtists
        | false -> tracks)
      &=|> (List.takeSafe (preset.Settings.Size |> PresetSettings.Size.value))
      &=|&> (saveTracks preset)
      &=|> (fun _ -> preset))

  let queueRun
    (loadPreset: Preset.Get)
    (validatePreset: Preset.Validate)
    (queueRun': PresetRepo.QueueRun)
    : Preset.QueueRun =
    loadPreset
    >> Task.map validatePreset
    >> TaskResult.taskTap (fun p -> queueRun' p.Id)

[<RequireQualifiedAccess>]
module User =
  let get (load: UserRepo.Load) : User.Get = load

  let setCurrentPreset (load: UserRepo.Load) (update: UserRepo.Update) : User.SetCurrentPreset =
    fun userId presetId ->
      userId
      |> load
      |> Task.map (fun u ->
        { u with
            CurrentPresetId = Some presetId })
      |> Task.bind update

  let removePreset (load: UserRepo.Load) (removePreset: Preset.Remove) (update: UserRepo.Update) : User.RemovePreset =
    fun userId presetId ->
      userId
      |> load
      |> Task.map (fun u ->
        { u with
            Presets = u.Presets |> List.filter (fun p -> p.Id <> presetId)
            CurrentPresetId = if u.CurrentPresetId = Some presetId then None else u.CurrentPresetId })
      |> Task.bind update
      |> Task.bind (fun _ -> removePreset presetId)

  let createIfNotExists (exists: UserRepo.Exists) (create: UserRepo.Create) : User.CreateIfNotExists =
    fun userId ->
      exists userId
      |> Task.bind (function
        | true -> Task.FromResult()
        | false -> User.create userId |> create)

  let setCurrentPresetSize (load: UserRepo.Load) (setPresetSize: PresetSettings.SetPresetSize) : User.SetCurrentPresetSize =
    fun userId size ->
      userId
      |> load
      |> Task.map (fun u -> u.CurrentPresetId |> Option.get)
      |> Task.bind (fun presetId -> setPresetSize presetId size)

  let createPreset (savePreset: PresetRepo.Save) (loadUser: UserRepo.Load) (updateUser: UserRepo.Update) : User.CreatePreset =
    fun userId name -> task {
      let! user = loadUser userId

      let! newPreset = Preset.create savePreset name

      let updatedUser =
        { user with
            Presets = SimplePreset.fromPreset newPreset :: user.Presets}

      do! updateUser updatedUser

      return newPreset
    }

[<RequireQualifiedAccess>]
module ExcludedPlaylist =
  let private updatePresetPlaylist (loadPreset: PresetRepo.Load) (updatePreset: PresetRepo.Save) enable =
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

  let remove (loadPreset: PresetRepo.Load) (updatePreset: PresetRepo.Save) : ExcludedPlaylist.Remove =
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
  type ParseId = Playlist.RawPlaylistId -> Result<PlaylistId, Playlist.IdParsingError>
  type IncludeInStorage = IncludedPlaylist -> Async<IncludedPlaylist>
  type ExcludeInStorage = ExcludedPlaylist -> Async<ExcludedPlaylist>
  type TargetInStorage = TargetedPlaylist -> Async<TargetedPlaylist>

  type CountTracks = PlaylistId -> Task<int64>

  let includePlaylist
    (parseId: ParseId)
    (loadPlaylist: PlaylistRepo.Load)
    (loadPreset: PresetRepo.Load)
    (updatePreset: PresetRepo.Save)
    : Playlist.IncludePlaylist =
    let parseId = parseId >> Result.mapError Playlist.IncludePlaylistError.IdParsing

    let loadPlaylist =
      loadPlaylist
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
      |> Result.taskBind loadPlaylist
      |> TaskResult.map IncludedPlaylist.fromSpotifyPlaylist
      |> TaskResult.taskMap updatePreset

  let excludePlaylist
    (parseId: ParseId)
    (loadPlaylist: PlaylistRepo.Load)
    (loadPreset: PresetRepo.Load)
    (updatePreset: PresetRepo.Save)
    : Playlist.ExcludePlaylist =
    let parseId = parseId >> Result.mapError Playlist.ExcludePlaylistError.IdParsing

    let loadPlaylist =
      loadPlaylist
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
      |> Result.taskBind loadPlaylist
      |> TaskResult.map ExcludedPlaylist.fromSpotifyPlaylist
      |> TaskResult.taskMap updatePreset

  let targetPlaylist
    (parseId: ParseId)
    (loadPlaylist: PlaylistRepo.Load)
    (loadPreset: PresetRepo.Load)
    (updatePreset: PresetRepo.Save)
    : Playlist.TargetPlaylist =
    let parseId = parseId >> Result.mapError Playlist.TargetPlaylistError.IdParsing

    let loadPlaylist =
      loadPlaylist
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
      |> Result.taskBind loadPlaylist
      |> Task.map (Result.bind checkAccess)
      |> TaskResult.taskMap updatePreset

[<RequireQualifiedAccess>]
module TargetedPlaylist =
  let private updatePresetPlaylist (loadPreset: PresetRepo.Load) (updatePreset: PresetRepo.Save) enable =
    fun presetId playlistId ->
      task {
        let! preset = loadPreset presetId

        let playlist = preset.TargetedPlaylists |> List.find (fun p -> p.Id = playlistId)
        let updatedPlaylist = { playlist with Enabled = enable }

        let updatedPreset =
          { preset with
              TargetedPlaylists =
                preset.TargetedPlaylists
                |> List.except [ playlist ]
                |> List.append [ updatedPlaylist ] }

        return! updatePreset updatedPreset
      }

  let enable loadPreset updatePreset : TargetedPlaylist.Enable =
    updatePresetPlaylist loadPreset updatePreset true

  let disable loadPreset updatePreset : TargetedPlaylist.Disable =
    updatePresetPlaylist loadPreset updatePreset false

  let private setPlaylistOverwriting (loadPreset: PresetRepo.Load) (updatePreset: PresetRepo.Save) overwriting =
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

  let overwriteTracks (loadPreset: PresetRepo.Load) (updatePreset: PresetRepo.Save) : TargetedPlaylist.OverwriteTracks =
    setPlaylistOverwriting loadPreset updatePreset true

  let appendTracks (loadPreset: PresetRepo.Load) (updatePreset: PresetRepo.Save) : TargetedPlaylist.AppendTracks =
    setPlaylistOverwriting loadPreset updatePreset false

  let remove (loadPreset: PresetRepo.Load) (updatePreset: PresetRepo.Save) : TargetedPlaylist.Remove =
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
