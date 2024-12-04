namespace Infrastructure.Workflows

open System
open MusicPlatform
open System.Text.RegularExpressions
open Domain.Core
open Domain.Workflows
open Infrastructure.Core

[<RequireQualifiedAccess>]
module Playlist =
  let countTracks telemetryClient multiplexer : Playlist.CountTracks =
    Infrastructure.Cache.Redis.Playlist.countTracks telemetryClient multiplexer