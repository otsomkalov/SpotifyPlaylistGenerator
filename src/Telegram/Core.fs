module Telegram.Core

open System.Threading.Tasks
open Domain.Core
open Microsoft.FSharp.Core
open otsom.fs.Telegram.Bot.Core

type AnswerCallbackQuery = string -> Task<unit>
type Page = Page of int

type SetCurrentPreset = UserId -> PresetId -> Task<unit>

type SendSettingsMessage = UserId -> Task<unit>

type SendPresetInfo = PresetId -> Task<unit>

[<RequireQualifiedAccess>]
module Playlist =
  type Include = UserId -> Playlist.RawPlaylistId -> Task<unit>
  type Exclude = UserId -> Playlist.RawPlaylistId -> Task<unit>
  type Target = UserId -> Playlist.RawPlaylistId -> Task<unit>
  type QueueGeneration = UserId -> Task<unit>

[<RequireQualifiedAccess>]
module User =
  type ListPresets = UserId -> Task<unit>
  type ShowCurrentPreset = UserId -> Task<unit>
  type RemovePreset = UserId -> PresetId -> Task<unit>
  type SetCurrentPresetSize = UserId -> PresetSettings.RawPlaylistSize -> Task<unit>

[<RequireQualifiedAccess>]
type IncludedPlaylistActions =
  | List of presetId: PresetId * page: Page
  | Show of presetId: PresetId * playlistId: ReadablePlaylistId
  | Remove of presetId: PresetId * playlistId: ReadablePlaylistId

[<RequireQualifiedAccess>]
type ExcludedPlaylistActions =
  | List of presetId: PresetId * page: Page
  | Show of presetId: PresetId * playlistId: ReadablePlaylistId
  | Remove of presetId: PresetId * playlistId: ReadablePlaylistId

[<RequireQualifiedAccess>]
type TargetedPlaylistActions =
  | List of presetId: PresetId * page: Page
  | Show of presetId: PresetId * playlistId: WritablePlaylistId
  | Remove of presetId: PresetId * playlistId: WritablePlaylistId

[<RequireQualifiedAccess>]
type PresetSettingsActions =
  | EnableUniqueArtists of presetId: PresetId
  | DisableUniqueArtists of presetId: PresetId

  | EnableRecommendations of presetId: PresetId
  | DisableRecommendations of presetId: PresetId

  | IncludeLikedTracks of presetId: PresetId
  | ExcludeLikedTracks of presetId: PresetId
  | IgnoreLikedTracks of presetId: PresetId

[<RequireQualifiedAccess>]
type UserActions =
  | ListPresets of userId: UserId

[<RequireQualifiedAccess>]
type Action =

  | IncludedPlaylist of IncludedPlaylistActions
  | ExcludedPlaylist of ExcludedPlaylistActions
  | TargetedPlaylist of TargetedPlaylistActions

  | PresetSettings of PresetSettingsActions

  | EnableIncludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId
  | DisableIncludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId

  | EnableExcludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId
  | DisableExcludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId

  | AppendToTargetedPlaylist of presetId: PresetId * playlistId: WritablePlaylistId
  | OverwriteTargetedPlaylist of presetId: PresetId * playlistId: WritablePlaylistId

  | ShowPresetInfo of presetId: PresetId
  | SetCurrentPreset of presetId: PresetId
  | RemovePreset of presetId: PresetId

  | AskForPlaylistSize

  | ShowUserPresets

type ParseAction = string -> Action

type AuthState =
  | Authorized
  | Unauthorized

[<RequireQualifiedAccess>]
module Message =
  type CreatePreset = string -> Task<unit>

[<RequireQualifiedAccess>]
module IncludedPlaylist =
  type List = PresetId -> Page -> Task<unit>
  type Show = PresetId -> ReadablePlaylistId -> Task<unit>

[<RequireQualifiedAccess>]
module ExcludedPlaylist =
  type List = PresetId -> Page -> Task<unit>
  type Show = PresetId -> ReadablePlaylistId -> Task<unit>

[<RequireQualifiedAccess>]
module TargetedPlaylist =
  type List = PresetId -> Page -> Task<unit>
  type Show = PresetId -> WritablePlaylistId -> Task<unit>