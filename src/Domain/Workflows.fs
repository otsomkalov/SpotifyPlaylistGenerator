module Domain.Workflows

open System.Threading.Tasks
open Domain.Core
open Domain.Extensions
open Microsoft.Extensions.Logging
open Microsoft.FSharp.Control

[<RequireQualifiedAccess>]
module UserId =
  let value (UserId id) = id

[<RequireQualifiedAccess>]
module PresetId =
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
  type ListLikedTracks = Async<string list>
  type LoadCurrentPreset = UserId -> Async<Preset>
  type LoadPreset = PresetId -> Async<SimplePreset>
  type GetCurrentPresetId = UserId -> Async<PresetId>

[<RequireQualifiedAccess>]
module Preset =
  type Load = PresetId -> Async<Preset>
  type UpdateSettings = PresetId -> PresetSettings.PresetSettings -> Task<unit>
  type GetRecommendations = int -> string list -> Task<string list>

  let validate: Preset.Validate =
    fun preset ->
      match preset.IncludedPlaylists, preset.Settings.LikedTracksHandling, preset.TargetedPlaylists with
      | [], PresetSettings.LikedTracksHandling.Include, [] ->
        [ Preset.ValidationError.NoTargetedPlaylists ]
        |> Preset.ValidationResult.Errors
      | [], _, [] ->
        [ Preset.ValidationError.NoIncludedPlaylists
          Preset.ValidationError.NoTargetedPlaylists
        ]
        |> Preset.ValidationResult.Errors
      | [], PresetSettings.LikedTracksHandling.Exclude, _ ->
        [ Preset.ValidationError.NoIncludedPlaylists ]
        |> Preset.ValidationResult.Errors
      | [], PresetSettings.LikedTracksHandling.Ignore, _ ->
        [ Preset.ValidationError.NoIncludedPlaylists ]
        |> Preset.ValidationResult.Errors
      | _ -> Preset.ValidationResult.Ok

[<RequireQualifiedAccess>]
module PresetSettings =
  type Load = UserId -> Task<PresetSettings.PresetSettings>

  type Update = UserId -> PresetSettings.PresetSettings -> Task

  let setPlaylistSize (loadPresetSettings: Load) (updatePresetSettings: Update) : PresetSettings.SetPlaylistSize =
    fun userId playlistSize ->
      task {
        let! presetSettings = loadPresetSettings userId

        let updatedSettings = { presetSettings with PlaylistSize = playlistSize }

        do! updatePresetSettings userId updatedSettings
      }

[<RequireQualifiedAccess>]
module Playlist =
  type ListTracks = ReadablePlaylistId -> Async<string list>

  type UpdateTracks = TargetedPlaylist -> TrackId list -> Async<unit>

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
    (includeInStorage: IncludeInStorage)
    : Playlist.IncludePlaylist =
    let parseId = parseId >> Result.mapError Playlist.IncludePlaylistError.IdParsing

    let loadFromSpotify =
      (loadFromSpotify >> Async.AwaitTask)
      >> AsyncResult.mapError Playlist.IncludePlaylistError.MissingFromSpotify

    parseId
    >> Result.asyncBind loadFromSpotify
    >> AsyncResult.map IncludedPlaylist.fromSpotifyPlaylist
    >> AsyncResult.asyncMap includeInStorage

  let excludePlaylist
    (parseId: ParseId)
    (loadFromSpotify: LoadFromSpotify)
    (excludeInStorage: ExcludeInStorage)
    : Playlist.ExcludePlaylist =
    let parseId = parseId >> Result.mapError Playlist.ExcludePlaylistError.IdParsing

    let loadFromSpotify =
      (loadFromSpotify >> Async.AwaitTask)
      >> AsyncResult.mapError Playlist.ExcludePlaylistError.MissingFromSpotify

    parseId
    >> Result.asyncBind loadFromSpotify
    >> AsyncResult.map ExcludedPlaylist.fromSpotifyPlaylist
    >> AsyncResult.asyncMap excludeInStorage

  let targetPlaylist
    (parseId: ParseId)
    (loadFromSpotify: LoadFromSpotify)
    (targetInStorage: TargetInStorage)
    : Playlist.TargetPlaylist =
    let parseId = parseId >> Result.mapError Playlist.TargetPlaylistError.IdParsing

    let loadFromSpotify =
      (loadFromSpotify >> Async.AwaitTask)
      >> AsyncResult.mapError Playlist.TargetPlaylistError.MissingFromSpotify

    let checkAccess playlist =
      let mapResult = TargetedPlaylist.fromSpotifyPlaylist playlist

      match mapResult with
      | Some r -> Ok r
      | None -> Playlist.AccessError() |> Playlist.TargetPlaylistError.AccessError |> Error

    parseId
    >> Result.asyncBind loadFromSpotify
    >> AsyncResult.bindSync checkAccess
    >> AsyncResult.asyncMap targetInStorage

  type Shuffler = string list -> string list

  let generate
    (logger: ILogger)
    (listPlaylistTracks: ListTracks)
    (listLikedTracks: User.ListLikedTracks)
    (loadPreset: Preset.Load)
    (updateTargetedPlaylist: UpdateTracks)
    (shuffler: Shuffler)
    (getRecommendations: Preset.GetRecommendations)
    : Playlist.Generate =
    fun presetId ->
      async {
        let! preset = loadPreset presetId

        let! likedTracks = listLikedTracks

        let! includedTracks =
          preset.IncludedPlaylists
          |> Seq.filter (fun p -> p.Enabled)
          |> Seq.map (fun p -> p.Id)
          |> Seq.map listPlaylistTracks
          |> Async.Parallel
          |> Async.map List.concat

        logger.LogInformation(
          "User with Telegram id {TelegramId} has {SourceTracksCount} source tracks in preset {PresetId}",
          preset.UserId |> UserId.value,
          includedTracks.Length,
          presetId |> PresetId.value
        )

        let! excludedTracks =
          preset.ExcludedPlaylist
          |> Seq.filter (fun p -> p.Enabled)
          |> Seq.map (fun p -> p.Id)
          |> Seq.map listPlaylistTracks
          |> Async.Parallel
          |> Async.map List.concat

        let excludedTracksIds, includedTracksIds =
          match preset.Settings.LikedTracksHandling with
          | PresetSettings.LikedTracksHandling.Include -> excludedTracks, includedTracks @ likedTracks
          | PresetSettings.LikedTracksHandling.Exclude -> likedTracks @ excludedTracks, includedTracks
          | PresetSettings.LikedTracksHandling.Ignore -> excludedTracks, includedTracks

        logger.LogInformation(
          "User with Telegram id {TelegramId} has {IncludedTracksCount} included tracks in preset {PresetId}",
          preset.UserId |> UserId.value,
          includedTracksIds.Length,
          presetId |> PresetId.value
        )

        logger.LogInformation(
          "User with Telegram id {TelegramId} has {ExcludedTracksCount} excluded tracks in preset {PresetId}",
          preset.UserId |> UserId.value,
          excludedTracksIds.Length,
          presetId |> PresetId.value
        )

        let potentialTracksIds = includedTracksIds |> List.except excludedTracksIds |> shuffler

        let! potentialTracksIds =
          if preset.Settings.RecommendationsEnabled then
            potentialTracksIds
            |> List.take 5
            |> (getRecommendations 100)
            |> Task.map ((@) potentialTracksIds)
            |> Async.AwaitTask
          else
            potentialTracksIds |> async.Return

        logger.LogInformation(
          "User with Telegram id {TelegramId} has {RecommendedTracksCount} recommended tracks in preset {PresetId}",
          preset.UserId |> UserId.value,
          includedTracks.Length,
          presetId |> PresetId.value
        )

        logger.LogInformation(
          "User with Telegram id {TelegramId} has {PotentialTracksCount} potential tracks in preset {PresetId}",
          preset.UserId |> UserId.value,
          potentialTracksIds.Length,
          presetId |> PresetId.value
        )

        let tracksIdsToImport =
          potentialTracksIds
          |> List.take (preset.Settings.PlaylistSize |> PlaylistSize.value)
          |> List.map TrackId

        for playlist in preset.TargetedPlaylists |> Seq.filter (fun p -> p.Enabled) do
          do! updateTargetedPlaylist playlist tracksIdsToImport

        return Ok()
      }

[<RequireQualifiedAccess>]
module IncludedPlaylist =
  type Enable = PresetId -> ReadablePlaylistId -> Task<unit>

  let enable (enableIncludedPlaylist: Enable) : IncludedPlaylist.Enable =
    enableIncludedPlaylist

  type Disable = PresetId -> ReadablePlaylistId -> Task<unit>

  let disable (disableIncludedPlaylist: Disable) : IncludedPlaylist.Disable =
    disableIncludedPlaylist

[<RequireQualifiedAccess>]
module TargetedPlaylist =
  type Update = PresetId -> TargetedPlaylist -> Task<unit>
  type Remove = PresetId -> TargetedPlaylistId -> Task<unit>

  let overwriteTargetedPlaylist (loadPreset: Preset.Load) (updatePlaylist: Update) : TargetedPlaylist.OverwriteTracks =
    fun presetId targetedPlaylistId ->
      task {
        let! preset = loadPreset presetId

        let targetPlaylist =
          preset.TargetedPlaylists |> List.find (fun p -> p.Id = targetedPlaylistId)

        let updatedPlaylist = { targetPlaylist with Overwrite = true }

        do! updatePlaylist presetId updatedPlaylist
      }

  let appendToTargetedPlaylist (loadPreset: Preset.Load) (updatePlaylist: Update) : TargetedPlaylist.AppendTracks =
    fun presetId targetPlaylistId ->
      task {
        let! preset = loadPreset presetId

        let targetPlaylist =
          preset.TargetedPlaylists |> List.find (fun p -> p.Id = targetPlaylistId)

        let updatedPlaylist = { targetPlaylist with Overwrite = false }

        do! updatePlaylist presetId updatedPlaylist
      }

  let remove (removeTargetedPlaylist: Remove) : TargetedPlaylist.Remove =
    removeTargetedPlaylist