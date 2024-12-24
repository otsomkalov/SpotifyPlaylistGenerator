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
  let fromPreset (preset: Preset) : SimplePreset = { Id = preset.Id; Name = preset.Name }

[<RequireQualifiedAccess>]
module PresetSettings =
  let private setUniqueArtists (presetRepo: #ILoadPreset & #ISavePreset) =
    fun uniqueArtists ->
      presetRepo.LoadPreset
      >> Task.map (fun preset ->
        { preset with
            Settings =
              { preset.Settings with
                  UniqueArtists = uniqueArtists } })
      >> Task.bind presetRepo.SavePreset

  let enableUniqueArtists presetRepo : PresetSettings.EnableUniqueArtists = setUniqueArtists presetRepo true

  let disableUniqueArtists presetRepo : PresetSettings.DisableUniqueArtists = setUniqueArtists presetRepo false

  let private setRecommendations (presetRepo: #ILoadPreset & #ISavePreset) =
    fun enabled ->
      presetRepo.LoadPreset
      >> Task.map (fun preset ->
        { preset with
            Settings =
              { preset.Settings with
                  RecommendationsEnabled = enabled } })
      >> Task.bind presetRepo.SavePreset

  let enableRecommendations presetRepo : PresetSettings.EnableRecommendations = setRecommendations presetRepo true

  let disableRecommendations presetRepo: PresetSettings.DisableRecommendations = setRecommendations presetRepo false

  let private setLikedTracksHandling (presetRepo: #ILoadPreset & #ISavePreset) =
    fun handling presetId ->
      presetId
      |> presetRepo.LoadPreset
      |> Task.map (fun p ->
        { p with
            Settings =
              { p.Settings with
                  LikedTracksHandling = handling } })
      |> Task.bind presetRepo.SavePreset

  let includeLikedTracks presetRepo : PresetSettings.IncludeLikedTracks =
    setLikedTracksHandling presetRepo PresetSettings.LikedTracksHandling.Include

  let excludeLikedTracks presetRepo : PresetSettings.ExcludeLikedTracks =
    setLikedTracksHandling presetRepo PresetSettings.LikedTracksHandling.Exclude

  let ignoreLikedTracks presetRepo : PresetSettings.IgnoreLikedTracks =
    setLikedTracksHandling presetRepo PresetSettings.LikedTracksHandling.Ignore

  let setPresetSize (presetRepo: #ILoadPreset & #ISavePreset) : PresetSettings.SetPresetSize =
    fun presetId size ->
      size
      |> PresetSettings.Size.tryParse
      |> Result.taskMap (fun s ->
        presetId
        |> presetRepo.LoadPreset
        |> Task.map (fun p ->
          { p with
              Settings = { p.Settings with Size = s } })
        |> Task.bind presetRepo.SavePreset)

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

  let private updatePresetPlaylist (presetRepo: #ILoadPreset & #ISavePreset) enable =
    fun presetId playlistId ->
      task {
        let! preset = presetRepo.LoadPreset presetId

        let playlist = preset.IncludedPlaylists |> List.find (fun p -> p.Id = playlistId)
        let updatedPlaylist = { playlist with Enabled = enable }

        let updatedPreset =
          { preset with
              IncludedPlaylists =
                preset.IncludedPlaylists
                |> List.except [ playlist ]
                |> List.append [ updatedPlaylist ] }

        return! presetRepo.SavePreset updatedPreset
      }

  let enable presetRepo : IncludedPlaylist.Enable =
    updatePresetPlaylist presetRepo true

  let disable presetRepo : IncludedPlaylist.Disable =
    updatePresetPlaylist presetRepo false

  let remove (presetRepo: #ILoadPreset & #ISavePreset) : IncludedPlaylist.Remove =
    fun presetId includedPlaylistId ->
      task {
        let! preset = presetRepo.LoadPreset presetId

        let includedPlaylists =
          preset.IncludedPlaylists |> List.filter (fun p -> p.Id <> includedPlaylistId)

        let updatedPreset =
          { preset with
              IncludedPlaylists = includedPlaylists }

        return! presetRepo.SavePreset updatedPreset
      }

[<RequireQualifiedAccess>]
module Preset =
  type UpdateSettings = PresetId -> PresetSettings.PresetSettings -> Task<unit>

  let get (presetRepo: #ILoadPreset) : Preset.Get =
    presetRepo.LoadPreset

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

  let create (presetRepo: #ISavePreset) : Preset.Create =
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

        do! presetRepo.SavePreset newPreset

        return newPreset
      }

  let remove (removePreset: Preset.Remove) : Preset.Remove =
    fun presetId ->
      removePreset presetId

  type RunIO =
    { ListExcludedTracks: PresetRepo.ListExcludedTracks
      AppendTracks: Playlist.AddTracks
      ReplaceTracks: Playlist.ReplaceTracks
      GetRecommendations: Track.GetRecommendations
      Shuffler: Track list -> Track list }

  let run (presetEnv: #ILoadPreset) (env: #IListLikedTracks) (io: RunIO) : Preset.Run =

    let saveTracks preset =
      fun (tracks: Track list) ->
        preset.TargetedPlaylists
        |> Seq.filter _.Enabled
        |> Seq.map (fun p ->
          match p.Overwrite with
          | true -> io.ReplaceTracks (p.Id |> WritablePlaylistId.value) tracks
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

    presetEnv.LoadPreset
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
  let get (userRepo: #ILoadUser) : User.Get = userRepo.LoadUser

  let setCurrentPreset (userRepo: #ILoadUser & #ISaveUser) : User.SetCurrentPreset =
    fun userId presetId ->
      userId
      |> userRepo.LoadUser
      |> Task.map (fun u ->
        { u with
            CurrentPresetId = Some presetId })
      |> Task.bind userRepo.SaveUser

  let removePreset (userRepo: #ILoadUser & #ISaveUser) (removePreset: Preset.Remove) : User.RemovePreset =
    fun userId presetId ->
      userId
      |> userRepo.LoadUser
      |> Task.map (fun u ->
        { u with
            Presets = u.Presets |> List.filter (fun p -> p.Id <> presetId)
            CurrentPresetId = if u.CurrentPresetId = Some presetId then None else u.CurrentPresetId })
      |> Task.bind userRepo.SaveUser
      |> Task.bind (fun _ -> removePreset presetId)

  let createIfNotExists (exists: UserRepo.Exists) (create: UserRepo.Create) : User.CreateIfNotExists =
    fun userId ->
      exists userId
      |> Task.bind (function
        | true -> Task.FromResult()
        | false -> User.create userId |> create)

  let setCurrentPresetSize (userRepo: #ILoadUser) (setPresetSize: PresetSettings.SetPresetSize) : User.SetCurrentPresetSize =
    fun userId size ->
      userId
      |> userRepo.LoadUser
      |> Task.map (fun u -> u.CurrentPresetId |> Option.get)
      |> Task.bind (fun presetId -> setPresetSize presetId size)

  let createPreset (presetRepo: #ISavePreset) (userRepo: #ILoadUser & #ISaveUser) : User.CreatePreset =
    fun userId name -> task {
      let! user = userRepo.LoadUser userId

      let! newPreset = Preset.create presetRepo name

      let updatedUser =
        { user with
            Presets = SimplePreset.fromPreset newPreset :: user.Presets}

      do! userRepo.SaveUser updatedUser

      return newPreset
    }

[<RequireQualifiedAccess>]
module ExcludedPlaylist =
  let private updatePresetPlaylist (presetRepo: #ILoadPreset & #ISavePreset) enable =
    fun presetId playlistId ->
      task {
        let! preset = presetRepo.LoadPreset presetId

        let playlist = preset.ExcludedPlaylists |> List.find (fun p -> p.Id = playlistId)
        let updatedPlaylist = { playlist with Enabled = enable }

        let updatedPreset =
          { preset with
              ExcludedPlaylists =
                preset.ExcludedPlaylists
                |> List.except [ playlist ]
                |> List.append [ updatedPlaylist ] }

        return! presetRepo.SavePreset updatedPreset
      }

  let enable presetRepo : ExcludedPlaylist.Enable =
    updatePresetPlaylist presetRepo true

  let disable presetRepo : ExcludedPlaylist.Disable =
    updatePresetPlaylist presetRepo false

  let remove (presetRepo: #ILoadPreset & #ISavePreset) : ExcludedPlaylist.Remove =
    fun presetId excludedPlaylistId ->
      task {
        let! preset = presetRepo.LoadPreset presetId

        let excludedPlaylists =
          preset.ExcludedPlaylists |> List.filter (fun p -> p.Id <> excludedPlaylistId)

        let updatedPreset =
          { preset with
              ExcludedPlaylists = excludedPlaylists }

        return! presetRepo.SavePreset updatedPreset
      }

[<RequireQualifiedAccess>]
module Playlist =
  type IncludeInStorage = IncludedPlaylist -> Async<IncludedPlaylist>
  type ExcludeInStorage = ExcludedPlaylist -> Async<ExcludedPlaylist>
  type TargetInStorage = TargetedPlaylist -> Async<TargetedPlaylist>

  type CountTracks = PlaylistId -> Task<int64>

  let includePlaylist
    (musicPlatform: #ILoadPlaylist option)
    (parseId: Playlist.ParseId)
    (presetRepo: #ILoadPreset & #ISavePreset)
    : Playlist.IncludePlaylist =
    let parseId = parseId >> Result.mapError Playlist.IncludePlaylistError.IdParsing

    let loadPlaylist (mp: #ILoadPlaylist) =
      mp.LoadPlaylist
      >> TaskResult.mapError Playlist.IncludePlaylistError.Load

    let includePlaylist' mp =
      fun presetId rawPlaylistId ->
        let updatePreset playlist =
          task {
            let! preset = presetRepo.LoadPreset presetId

            let updatedIncludedPlaylists = preset.IncludedPlaylists |> List.append [ playlist ]

            let updatedPreset =
              { preset with
                  IncludedPlaylists = updatedIncludedPlaylists }

            do! presetRepo.SavePreset updatedPreset

            return playlist
          }

        rawPlaylistId
        |> parseId
        |> Result.taskBind (loadPlaylist mp)
        |> TaskResult.map IncludedPlaylist.fromSpotifyPlaylist
        |> TaskResult.taskMap updatePreset

    fun presetId rawPlaylistId ->
      match musicPlatform with
      | Some mp -> includePlaylist' mp presetId rawPlaylistId
      | None -> Playlist.IncludePlaylistError.Unauthorized |> Error |> Task.FromResult

  let excludePlaylist
    (musicPlatform: #ILoadPlaylist option)
    (parseId: Playlist.ParseId)
    (presetRepo: #ILoadPreset & #ISavePreset)
    : Playlist.ExcludePlaylist =
    let parseId = parseId >> Result.mapError Playlist.ExcludePlaylistError.IdParsing

    let loadPlaylist (mp: #ILoadPlaylist) =
      mp.LoadPlaylist
      >> TaskResult.mapError Playlist.ExcludePlaylistError.Load

    let excludePlaylist' mp =
      fun presetId rawPlaylistId ->
        let updatePreset playlist =
          task {
            let! preset = presetRepo.LoadPreset presetId

            let updatedExcludedPlaylists = preset.ExcludedPlaylists |> List.append [ playlist ]

            let updatedPreset =
              { preset with
                  ExcludedPlaylists = updatedExcludedPlaylists }

            do! presetRepo.SavePreset updatedPreset

            return playlist
          }

        rawPlaylistId
        |> parseId
        |> Result.taskBind (loadPlaylist mp)
        |> TaskResult.map ExcludedPlaylist.fromSpotifyPlaylist
        |> TaskResult.taskMap updatePreset

    fun presetId rawPlaylistId ->
      match musicPlatform with
      | Some mp -> excludePlaylist' mp presetId rawPlaylistId
      | None -> Playlist.ExcludePlaylistError.Unauthorized |> Error |> Task.FromResult

  let targetPlaylist
    (musicPlatform: #ILoadPlaylist option)
    (parseId: Playlist.ParseId)
    (presetRepo: #ILoadPreset & #ISavePreset)
    : Playlist.TargetPlaylist =
    let parseId = parseId >> Result.mapError Playlist.TargetPlaylistError.IdParsing

    let loadPlaylist (mp: #ILoadPlaylist) =
      mp.LoadPlaylist
      >> TaskResult.mapError Playlist.TargetPlaylistError.Load

    let checkAccess playlist =
      playlist
      |> TargetedPlaylist.fromSpotifyPlaylist
      |> Result.ofOption (Playlist.AccessError() |> Playlist.TargetPlaylistError.AccessError)

    let targetPlaylist' mp =
      fun presetId rawPlaylistId ->
        let updatePreset playlist =
          task {
            let! preset = presetRepo.LoadPreset presetId

            let updatedTargetedPlaylists = preset.TargetedPlaylists |> List.append [ playlist ]

            let updatedPreset =
              { preset with
                  TargetedPlaylists = updatedTargetedPlaylists }

            do! presetRepo.SavePreset updatedPreset

            return playlist
          }

        rawPlaylistId
        |> parseId
        |> Result.taskBind (loadPlaylist mp)
        |> Task.map (Result.bind checkAccess)
        |> TaskResult.taskMap updatePreset

    fun presetId rawPlaylistId ->
        match musicPlatform with
        | Some mp -> targetPlaylist' mp presetId rawPlaylistId
        | None -> Playlist.TargetPlaylistError.Unauthorized |> Error |> Task.FromResult

[<RequireQualifiedAccess>]
module TargetedPlaylist =
  let private updatePresetPlaylist (presetRepo: #ILoadPreset & #ISavePreset) enable =
    fun presetId playlistId ->
      task {
        let! preset = presetRepo.LoadPreset presetId

        let playlist = preset.TargetedPlaylists |> List.find (fun p -> p.Id = playlistId)
        let updatedPlaylist = { playlist with Enabled = enable }

        let updatedPreset =
          { preset with
              TargetedPlaylists =
                preset.TargetedPlaylists
                |> List.except [ playlist ]
                |> List.append [ updatedPlaylist ] }

        return! presetRepo.SavePreset updatedPreset
      }

  let enable presetRepo : TargetedPlaylist.Enable =
    updatePresetPlaylist presetRepo true

  let disable presetRepo : TargetedPlaylist.Disable =
    updatePresetPlaylist presetRepo false

  let private setPlaylistOverwriting (presetRepo: #ILoadPreset & #ISavePreset) overwriting =
    fun presetId targetedPlaylistId ->
      task {
        let! preset = presetRepo.LoadPreset presetId

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

        return! presetRepo.SavePreset updatedPreset
      }

  let overwriteTracks presetRepo : TargetedPlaylist.OverwriteTracks =
    setPlaylistOverwriting presetRepo true

  let appendTracks presetRepo : TargetedPlaylist.AppendTracks =
    setPlaylistOverwriting presetRepo false

  let remove (presetRepo: #ILoadPreset & #ISavePreset) : TargetedPlaylist.Remove =
    fun presetId targetPlaylistId ->
      task {
        let! preset = presetRepo.LoadPreset presetId

        let targetPlaylists =
          preset.TargetedPlaylists |> List.filter (fun p -> p.Id <> targetPlaylistId)

        let updatedPreset =
          { preset with
              TargetedPlaylists = targetPlaylists }

        return! presetRepo.SavePreset updatedPreset
      }
