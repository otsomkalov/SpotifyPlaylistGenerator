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
module User =
  type ListLikedTracks = Async<string list>
  type LoadCurrentPreset = UserId -> Async<Preset>

  type ListPresets = UserId -> Async<SimplePreset seq>
  type LoadPreset = PresetId -> Async<SimplePreset>

[<RequireQualifiedAccess>]
module Preset =
  type Load = PresetId -> Async<Preset>

  let validate: Preset.Validate =
    fun preset ->
      match preset.IncludedPlaylists, preset.TargetPlaylists with
      | [], [] ->
        [ Preset.ValidationError.NoIncludedPlaylists
          Preset.ValidationError.NoTargetPlaylists ]
        |> Preset.ValidationResult.Errors
      | [], _ -> [ Preset.ValidationError.NoIncludedPlaylists ] |> Preset.ValidationResult.Errors
      | _, [] -> [ Preset.ValidationError.NoTargetPlaylists ] |> Preset.ValidationResult.Errors
      | _, _ -> Preset.ValidationResult.Ok

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

  let setLikedTracksHandling (loadPresetSettings: Load) (updateInStorage: Update) : PresetSettings.SetLikedTracksHandling =
    fun userId likedTracksHandling ->
      task {
        let! presetSettings = loadPresetSettings userId

        let updatedSettings = { presetSettings with LikedTracksHandling = likedTracksHandling }

        do! updateInStorage userId updatedSettings
      }

[<RequireQualifiedAccess>]
module Playlist =
  type ListTracks = ReadablePlaylistId -> Async<string list>

  type Update = TargetPlaylist -> TrackId list -> Async<unit>

  type ParsedPlaylistId = ParsedPlaylistId of string

  type ParseId = Playlist.RawPlaylistId -> Result<ParsedPlaylistId, Playlist.IdParsingError>

  type CheckExistsInSpotify = ParsedPlaylistId -> Async<Result<SpotifyPlaylist, Playlist.MissingFromSpotifyError>>

  type CheckWriteAccess = SpotifyPlaylist -> Async<Result<WritablePlaylist, Playlist.AccessError>>

  type IncludeInStorage = ReadablePlaylist -> Async<ReadablePlaylist>
  type ExcludeInStorage = ReadablePlaylist -> Async<ReadablePlaylist>
  type TargetInStorage = WritablePlaylist -> Async<WritablePlaylist>

  let includePlaylist
    (parseId: ParseId)
    (existsInSpotify: CheckExistsInSpotify)
    (includeInStorage: IncludeInStorage)
    : Playlist.IncludePlaylist =
    let parseId = parseId >> Result.mapError Playlist.IncludePlaylistError.IdParsing

    let existsInSpotify =
      existsInSpotify
      >> AsyncResult.mapError Playlist.IncludePlaylistError.MissingFromSpotify

    parseId
    >> Result.asyncBind existsInSpotify
    >> AsyncResult.map ReadablePlaylist.fromSpotifyPlaylist
    >> AsyncResult.asyncMap includeInStorage

  let excludePlaylist
    (parseId: ParseId)
    (existsInSpotify: CheckExistsInSpotify)
    (excludeInStorage: ExcludeInStorage)
    : Playlist.ExcludePlaylist =
    let parseId = parseId >> Result.mapError Playlist.ExcludePlaylistError.IdParsing

    let existsInSpotify =
      existsInSpotify
      >> AsyncResult.mapError Playlist.ExcludePlaylistError.MissingFromSpotify

    parseId
    >> Result.asyncBind existsInSpotify
    >> AsyncResult.map ReadablePlaylist.fromSpotifyPlaylist
    >> AsyncResult.asyncMap excludeInStorage

  let targetPlaylist
    (parseId: ParseId)
    (existsInSpotify: CheckExistsInSpotify)
    (checkWriteAccess: CheckWriteAccess)
    (targetInStorage: TargetInStorage)
    : Playlist.TargetPlaylist =
    let parseId = parseId >> Result.mapError Playlist.TargetPlaylistError.IdParsing

    let existsInSpotify =
      existsInSpotify
      >> AsyncResult.mapError Playlist.TargetPlaylistError.MissingFromSpotify

    let checkWriteAccess =
      checkWriteAccess
      >> AsyncResult.mapError Playlist.TargetPlaylistError.AccessError

    parseId
    >> Result.asyncBind existsInSpotify
    >> AsyncResult.bind checkWriteAccess
    >> AsyncResult.asyncMap targetInStorage

  type Shuffler = string list -> string list

  let generate
    (logger: ILogger)
    (listPlaylistTracks: ListTracks)
    (listLikedTracks: User.ListLikedTracks)
    (loadPreset: Preset.Load)
    (updateTargetPlaylist: Update)
    (shuffler: Shuffler)
    : Playlist.Generate =
    fun presetId ->
      async {
        let! preset = loadPreset presetId

        let! likedTracks = listLikedTracks

        let! includedTracks =
          preset.IncludedPlaylists
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

        let potentialTracksIds = includedTracksIds |> List.except excludedTracksIds

        logger.LogInformation(
          "User with Telegram id {TelegramId} has {PotentialTracksCount} potential tracks in preset {PresetId}",
          preset.UserId |> UserId.value,
          potentialTracksIds.Length,
          presetId |> PresetId.value
        )

        let tracksIdsToImport =
          potentialTracksIds
          |> shuffler
          |> List.take (preset.Settings.PlaylistSize |> PlaylistSize.value)
          |> List.map TrackId

        for playlist in preset.TargetPlaylists do
          do! updateTargetPlaylist playlist tracksIdsToImport

        return Ok()
      }

[<RequireQualifiedAccess>]
module TargetPlaylist =
  type AppendToTargetPlaylist = UserId -> WritablePlaylistId -> Task<unit>
  type OverwriteTargetPlaylist = UserId -> WritablePlaylistId -> Task<unit>
