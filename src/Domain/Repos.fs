module Domain.Repos

open System.Threading.Tasks
open Domain.Core

[<RequireQualifiedAccess>]
module PresetRepo =
  type Remove = PresetId -> Task<unit>
