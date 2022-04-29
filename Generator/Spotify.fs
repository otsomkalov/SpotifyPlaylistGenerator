module Spotify

type RawTrackId = RawTrackId of string

module RawTrackId =
    let create id = RawTrackId id
    let value (RawTrackId id) = id

type SpotifyTrackId = SpotifyTrackId of string

module SpotifyTrackId =
    let create (RawTrackId id) = SpotifyTrackId $"spotify:track:{id}"
    let value (SpotifyTrackId str) = str
    let rawValue (SpotifyTrackId str) = str.Split(":") |> Array.last |> RawTrackId.create

