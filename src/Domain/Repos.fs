module Domain.Repos

open System.Threading.Tasks
open Domain.Core
open MusicPlatform
open otsom.fs.Core

[<RequireQualifiedAccess>]
module PresetRepo =
  type Remove = PresetId -> Task<unit>

  type ListExcludedTracks = ExcludedPlaylist list -> Task<Track list>

  type QueueRun = PresetId -> Task<unit>

[<RequireQualifiedAccess>]
module UserRepo =
  type Exists = UserId -> Task<bool>
  type Create = User -> Task<unit>

  type ListLikedTracks = unit -> Task<Track list>

type IListPlaylistTracks =
  abstract member ListPlaylistTracks: PlaylistId -> Task<Track list>

type IListLikedTracks =
  abstract member ListLikedTracks : unit -> Task<Track list>

type ILoadPreset = abstract LoadPreset: presetId: PresetId -> Task<Preset>
type ISavePreset = abstract SavePreset: preset: Preset -> Task<unit>

type IPresetRepo =
  inherit ILoadPreset
  inherit ISavePreset

type ILoadUser = abstract LoadUser: userId: UserId -> Task<User>
type ISaveUser = abstract SaveUser: user: User -> Task<unit>

type IUserRepo =
  inherit ILoadUser
  inherit ISaveUser