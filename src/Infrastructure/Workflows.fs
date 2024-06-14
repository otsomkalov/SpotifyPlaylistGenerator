namespace Infrastructure.Workflows

open System
open Infrastructure
open MongoDB.Driver
open SpotifyAPI.Web
open System.Net
open System.Text.RegularExpressions
open Domain.Core
open Domain.Workflows
open Infrastructure.Core
open Infrastructure.Mapping
open StackExchange.Redis
open Infrastructure.Helpers.Spotify

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
      | SpotifyUri id -> id |> Playlist.ParsedPlaylistId |> Ok
      | Uri uri -> uri |> getPlaylistIdFromUri |> Playlist.ParsedPlaylistId |> Ok
      | PlaylistId id -> id |> Playlist.ParsedPlaylistId |> Ok
      | _ -> Playlist.IdParsingError() |> Error

  let loadFromSpotify (client: ISpotifyClient) : Playlist.LoadFromSpotify =
    fun playlistId ->
      let rawPlaylistId = playlistId |> ParsedPlaylistId.value

      task {
        try
          let! playlist = rawPlaylistId |> client.Playlists.Get

          let! currentUser = client.UserProfile.Current()

          let playlist =
            if playlist.Owner.Id = currentUser.Id then
              WriteableSpotifyPlaylist(
                { Id = playlist.Id |> PlaylistId
                  Name = playlist.Name }
              )
              |> SpotifyPlaylist.Writable
            else
              ReadableSpotifyPlaylist(
                { Id = playlist.Id |> PlaylistId
                  Name = playlist.Name }
              )
              |> SpotifyPlaylist.Readable

          return playlist |> Ok
        with ApiException e when e.Response.StatusCode = HttpStatusCode.NotFound ->
          return Playlist.MissingFromSpotifyError rawPlaylistId |> Error
      }

  let countTracks telemetryClient multiplexer : Playlist.CountTracks =
    Cache.Playlist.countTracks telemetryClient multiplexer

[<RequireQualifiedAccess>]
module Preset =
  let save (db: IMongoDatabase) : Preset.Save =
    fun preset ->
      task {
        let collection = db.GetCollection "presets"

        let dbPreset = preset |> Preset.toDb

        return! collection.InsertOneAsync(dbPreset)
      }