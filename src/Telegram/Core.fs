module Telegram.Core

open System.Threading.Tasks
open Domain.Core
open Microsoft.FSharp.Core
open otsom.fs.Telegram.Bot.Core

type AnswerCallbackQuery = string -> Task<unit>
type Page = Page of int

type SendUserPresets = UserId -> Task<unit>
type SendCurrentPresetInfo = UserId -> Task<unit>
type SetCurrentPreset = UserId -> PresetId -> Task<unit>

type SendSettingsMessage = UserId -> Task<unit>

type SendPresetInfo = PresetId -> Task<unit>
type ShowExcludedPlaylist = PresetId -> ReadablePlaylistId -> Task<unit>

type ShowTargetedPlaylist = PresetId -> WritablePlaylistId -> Task<unit>

[<RequireQualifiedAccess>]
module Playlist =
  type Include = UserId -> Playlist.RawPlaylistId -> Task<unit>
  type Exclude = UserId -> Playlist.RawPlaylistId -> Task<unit>
  type Target = UserId -> Playlist.RawPlaylistId -> Task<unit>
  type QueueGeneration = UserId -> Task<unit>

[<RequireQualifiedAccess>]
module User =
  type RemovePreset = UserId -> PresetId -> Task<unit>

[<RequireQualifiedAccess>]
type IncludedPlaylistActions =
  | List of presetId: PresetId * page: Page
  | Show of presetId: PresetId * playlistId: ReadablePlaylistId

[<RequireQualifiedAccess>]
type ExcludedPlaylistActions =
  | List of presetId: PresetId * page: Page

[<RequireQualifiedAccess>]
type TargetedPlaylistActions =
  | List of presetId: PresetId * page: Page

[<RequireQualifiedAccess>]
type Action =

  | IncludedPlaylist of IncludedPlaylistActions
  | ExcludedPlaylist of ExcludedPlaylistActions

  | EnableIncludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId
  | DisableIncludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId
  | RemoveIncludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId

  | ShowExcludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId
  | EnableExcludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId
  | DisableExcludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId
  | RemoveExcludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId

  | ShowTargetedPlaylists of presetId: PresetId * page: Page
  | ShowTargetedPlaylist of presetId: PresetId * playlistId: WritablePlaylistId
  | AppendToTargetedPlaylist of presetId: PresetId * playlistId: WritablePlaylistId
  | OverwriteTargetedPlaylist of presetId: PresetId * playlistId: WritablePlaylistId
  | RemoveTargetedPlaylist of presetId: PresetId * playlistId: WritablePlaylistId

  | ShowPresetInfo of presetId: PresetId
  | SetCurrentPreset of presetId: PresetId
  | RemovePreset of presetId: PresetId

  | AskForPlaylistSize

  | IncludeLikedTracks of presetId: PresetId
  | ExcludeLikedTracks of presetId: PresetId
  | IgnoreLikedTracks of presetId: PresetId

  | EnableRecommendations of presetId: PresetId
  | DisableRecommendations of presetId: PresetId

  | EnableUniqueArtists of presetId: PresetId
  | DisableUniqueArtists of presetId: PresetId

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

[<RequireQualifiedAccess>]
module TargetedPlaylist =
  type List = PresetId -> Page -> Task<unit>
