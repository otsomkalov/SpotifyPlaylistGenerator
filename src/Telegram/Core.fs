module Telegram.Core

open System.Threading.Tasks
open Domain.Core

type AnswerCallbackQuery = string -> Task<unit>
type Page = Page of int

// [<RequireQualifiedAccess>]
// module Message =
//   type Action =
//     |

type SendUserPresets = UserId -> Task<unit>
type SendCurrentPresetInfo = UserId -> Task<unit>
type SetCurrentPreset = UserId -> PresetId -> Task<unit>

type SendSettingsMessage = UserId -> Task<unit>

type SendPresetInfo = PresetId -> Task<unit>
type AskForPlaylistSize = UserId -> Task<unit>
type SetLikedTracksHandling = PresetId -> PresetSettings.LikedTracksHandling -> Task<unit>
type GetPresetMessage = PresetId -> Task<string * string * string>

type ShowIncludedPlaylists = PresetId -> Page -> Task<unit>
type ShowIncludedPlaylist = PresetId -> ReadablePlaylistId -> Task<unit>
type EnableIncludedPlaylist = PresetId -> ReadablePlaylistId -> Task<unit>
type DisableIncludedPlaylist = PresetId -> ReadablePlaylistId -> Task<unit>
type RemoveIncludedPlaylist = PresetId -> ReadablePlaylistId -> Task<unit>

type ShowExcludedPlaylists = PresetId -> Page -> Task<unit>
type ShowExcludedPlaylist = PresetId -> ReadablePlaylistId -> Task<unit>
type RemoveExcludedPlaylist = PresetId -> ReadablePlaylistId -> Task<unit>

type ShowTargetPlaylists = PresetId -> Page -> Task<unit>
type ShowTargetPlaylist = PresetId -> WritablePlaylistId -> Task<unit>
type AppendToTargetPlaylist = PresetId -> WritablePlaylistId -> Task<unit>
type OverwriteTargetPlaylist = PresetId -> WritablePlaylistId -> Task<unit>
type RemoveTargetPlaylist = PresetId -> WritablePlaylistId -> Task<unit>

[<RequireQualifiedAccess>]
type Action =

  | ShowIncludedPlaylists of presetId: PresetId * page: Page
  | ShowIncludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId
  | EnableIncludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId
  | DisableIncludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId
  | RemoveIncludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId

  | ShowExcludedPlaylists of presetId: PresetId * page: Page
  | ShowExcludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId
  | RemoveExcludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId

  | ShowTargetPlaylists of presetId: PresetId * page: Page
  | ShowTargetPlaylist of presetId: PresetId * playlistId: WritablePlaylistId
  | AppendToTargetPlaylist of presetId: PresetId * playlistId: WritablePlaylistId
  | OverwriteTargetPlaylist of presetId: PresetId * playlistId: WritablePlaylistId
  | RemoveTargetPlaylist of presetId: PresetId * playlistId: WritablePlaylistId

  | ShowPresetInfo of presetId: PresetId
  | SetCurrentPreset of presetId: PresetId

  | AskForPlaylistSize

  | IncludeLikedTracks of presetId: PresetId
  | ExcludeLikedTracks of presetId: PresetId
  | IgnoreLikedTracks of presetId: PresetId

type AuthState =
  | Authorized
  | Unauthorized

type CheckAuth = UserId -> Task<AuthState>