module Domain.Core

open System.Threading.Tasks

type UserId = UserId of int64
type ReadablePlaylistId = ReadablePlaylistId of string
type WritablePlaylistId = WritablePlaylistId of string

type User =
  { Id: UserId
    IncludedPlaylists: ReadablePlaylistId list
    TargetPlaylists: WritablePlaylistId list }

[<RequireQualifiedAccess>]
module ValidateUserPlaylists =
  type Error =
    | NoIncludedPlaylists
    | NoTargetPlaylists

  type Result =
    | Ok
    | Errors of Error list

  type Action = UserId -> Task<Result>
