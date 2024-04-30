module Domain.Repos

open System.Threading.Tasks
open Domain.Core
open otsom.fs.Telegram.Bot.Core

[<RequireQualifiedAccess>]
module PresetRepo =
  type Load = PresetId -> Task<Preset>

  type Remove = PresetId -> Task<unit>

[<RequireQualifiedAccess>]
module UserRepo =
  type Load = UserId -> Task<User>
