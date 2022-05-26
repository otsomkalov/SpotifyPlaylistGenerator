module Generator.Bot.ExtendedSpotify

open System.Collections.Generic
open Shared.Settings
open Shared.Spotify
open SpotifyAPI.Web

type IExtendedSpotify =
  inherit ISpotify

  abstract Settings: SpotifySettings

let getLoginUrl (env: #IExtendedSpotify) =
  let scopes =
    [ Scopes.PlaylistModifyPrivate
      Scopes.PlaylistModifyPublic
      Scopes.UserLibraryRead ]
    |> List<string>

  let loginRequest =
    LoginRequest(env.Settings.CallbackUrl, env.Settings.ClientId, LoginRequest.ResponseType.Code, Scope = scopes)

  loginRequest.ToUri().ToString()
