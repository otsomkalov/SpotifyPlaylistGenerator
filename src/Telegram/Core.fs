module Telegram.Core

open System.Threading.Tasks
open Domain.Core
open Microsoft.FSharp.Core
open MusicPlatform
open otsom.fs.Bot
open otsom.fs.Core
open otsom.fs.Telegram.Bot.Core

type AnswerCallbackQuery = unit -> Task<unit>
type ShowNotification = string -> Task<unit>
type Page = Page of int

type ChatMessageId = ChatMessageId of int

type SendLoginMessage = UserId -> Task<BotMessageId>

[<RequireQualifiedAccess>]
module Playlist =
  type Include = UserId -> Playlist.RawPlaylistId -> Task<unit>
  type Exclude = UserId -> Playlist.RawPlaylistId -> Task<unit>
  type Target = UserId -> Playlist.RawPlaylistId -> Task<unit>

[<RequireQualifiedAccess>]
module User =
  type ShowPresets = UserId -> Task<unit>
  type SendPresets = UserId -> Task<unit>
  type SendCurrentPreset = UserId -> Task<unit>
  type SendCurrentPresetSettings = UserId -> Task<unit>
  type RemovePreset = UserId -> PresetId -> Task<unit>
  type SetCurrentPreset = UserId -> PresetId -> Task<unit>
  type SetCurrentPresetSize = UserId -> PresetSettings.RawPresetSize -> Task<unit>
  type QueueCurrentPresetRun = UserId -> ChatMessageId -> Task<unit>
  type RunCurrentPreset = UserId -> Task<unit>
  type CreatePreset = UserId -> string -> Task<unit>

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
  | ListPresets of unit
  | SendCurrentPresetSettings of userId: UserId
  | QueueCurrentPresetGeneration of userId: UserId
  | GenerateCurrentPreset of presetId: PresetId

[<RequireQualifiedAccess>]
type PresetActions =
  | Show of presetId: PresetId
  | Run of presetId: PresetId

[<RequireQualifiedAccess>]
type Action =

  | IncludedPlaylist of IncludedPlaylistActions
  | ExcludedPlaylist of ExcludedPlaylistActions
  | TargetedPlaylist of TargetedPlaylistActions
  | Preset of PresetActions
  | User of UserActions

  | PresetSettings of PresetSettingsActions

  | EnableIncludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId
  | DisableIncludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId

  | EnableExcludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId
  | DisableExcludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId

  | AppendToTargetedPlaylist of presetId: PresetId * playlistId: WritablePlaylistId
  | OverwriteTargetedPlaylist of presetId: PresetId * playlistId: WritablePlaylistId

  | SetCurrentPreset of presetId: PresetId
  | RemovePreset of presetId: PresetId

  | AskForPresetSize

type ParseAction = string -> Action

type AuthState =
  | Authorized
  | Unauthorized

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

[<RequireQualifiedAccess>]
module Preset =
  type Show = PresetId -> Task<unit>
  type Run = PresetId -> Task<unit>

type Message = {
  ChatId: ChatId
  Text: string
}

type MessageHandler = Message -> Task<unit>

type MessageHandlerMatcher = Message -> MessageHandler option

type Chat = {Id: ChatId; UserId: UserId}