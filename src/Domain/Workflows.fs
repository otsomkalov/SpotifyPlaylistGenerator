module Domain.Workflows

open System.Threading.Tasks
open Domain.Core
open Domain.Extensions
open Microsoft.FSharp.Control

[<RequireQualifiedAccess>]
module User =
  type ListLikedTracks = Async<string list>
  type LoadCurrentPreset = UserId -> Async<Preset>

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

  type TryParseId = Playlist.RawPlaylistId -> Result<ParsedPlaylistId, Playlist.IncludePlaylistError>

  type CheckExistsInSpotify = ParsedPlaylistId -> Async<Result<SpotifyPlaylist, Playlist.MissingFromSpotifyError>>

  type CheckWriteAccess = SpotifyPlaylist -> Async<Result<WritablePlaylistId, Playlist.AccessError>>

  type IncludeInStorage = ReadablePlaylistId -> Async<unit>
  type ExcludeInStorage = ReadablePlaylistId -> Async<unit>
  type TargetInStorage = WritablePlaylistId -> Async<WritablePlaylistId>

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
    >> AsyncResult.map (fun p -> ReadablePlaylistId p.Id)
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
    >> AsyncResult.map (fun p -> ReadablePlaylistId p.Id)
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

[<RequireQualifiedAccess>]
module TargetPlaylist =
  type AppendToTargetPlaylist = UserId -> WritablePlaylistId -> Task<unit>
  type OverwriteTargetPlaylist = UserId -> WritablePlaylistId -> Task<unit>