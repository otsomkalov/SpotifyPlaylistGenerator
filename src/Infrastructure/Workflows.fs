namespace Infrastructure.Workflows

open System
open MusicPlatform
open System.Text.RegularExpressions
open Domain.Core
open Domain.Workflows
open Infrastructure.Core

[<RequireQualifiedAccess>]
module Playlist =
  let parseId: Playlist.ParseId =
    fun rawPlaylistId ->
      let getPlaylistIdFromUri (uri: Uri) = uri.Segments |> Array.last

      let (|Uri|_|) text =
        match Uri.TryCreate(text, UriKind.Absolute) with
        | true, uri -> Some uri
        | _ -> None

      let (|PlaylistId|_|) (text: string) =
        if Regex.IsMatch(text, "[A-z0-9]{22}") then
          Some text
        else
          None

      let (|SpotifyUri|_|) (text: string) =
        match text.Split(":") with
        | [| "spotify"; "playlist"; id |] -> Some(id)
        | _ -> None

      match rawPlaylistId |> RawPlaylistId.value with
      | SpotifyUri id -> id |> PlaylistId |> Ok
      | Uri uri -> uri |> getPlaylistIdFromUri |> PlaylistId |> Ok
      | PlaylistId id -> id |> PlaylistId |> Ok
      | id -> Playlist.IdParsingError(id) |> Error


  let countTracks telemetryClient multiplexer : Playlist.CountTracks =
    Infrastructure.Cache.Redis.Playlist.countTracks telemetryClient multiplexer