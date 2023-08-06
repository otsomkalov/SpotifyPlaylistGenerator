namespace Shared.Settings

open System

module SpotifySettings =
  [<Literal>]
  let SectionName = "Spotify"

type SpotifySettings() =
  member val ClientId = "" with get, set
  member val ClientSecret = "" with get, set
  member val CallbackUrl: Uri = null with get, set