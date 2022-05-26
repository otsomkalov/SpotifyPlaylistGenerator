module Shared.SpotifySettings

open Shared.Settings

[<Interface>]
type ISpotifySettings =
  abstract Settings: SpotifySettings

let callbackUrl (env: #ISpotifySettings) = env.Settings.CallbackUrl
let clientUrl (env: #ISpotifySettings) = env.Settings.ClientId
