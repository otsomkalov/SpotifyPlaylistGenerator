module Domain.Repos

open System.Threading.Tasks
open Domain.Core
open otsom.fs.Telegram.Bot.Core

[<RequireQualifiedAccess>]
module PresetRepo =
  type Load = PresetId -> Task<Preset>

  type Update = Preset -> Task<unit>
  type Remove = PresetId -> Task<unit>

  type ListIncludedTracks = IncludedPlaylist list -> Task<Track list>
  type ListExcludedTracks = ExcludedPlaylist list -> Task<Track list>

[<RequireQualifiedAccess>]
module TargetedPlaylistRepo =
  type AppendTracks = TargetedPlaylistId -> Track list -> Task<unit>
  type ReplaceTracks = TargetedPlaylistId -> Track list -> Task<unit>

[<RequireQualifiedAccess>]
module UserRepo =
  type Load = UserId -> Task<User>
  type Update = User -> Task<unit>
  type Exists = UserId -> Task<bool>
  type Create = User -> Task<unit>

[<RequireQualifiedAccess>]
module TrackRepo =
  type GetRecommendations = TrackId list -> Task<Track list>
