module Domain.Repos

open System.Threading.Tasks
open Domain.Core

[<RequireQualifiedAccess>]
module Preset =
  type Remove = PresetId -> Task<unit>
