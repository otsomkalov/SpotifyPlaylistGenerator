module Domain.Workflows

open System.Threading.Tasks
open Domain.Core

[<RequireQualifiedAccess>]
module ValidateUserPlaylists =
  type LoadUser = UserId -> Task<User>

  let validateUserPlaylists (loadUser: LoadUser) : ValidateUserPlaylists.Action =
    fun userId ->
      task {
        let! user = loadUser userId

        return
          match user.IncludedPlaylists, user.TargetPlaylists with
          | [], [] ->
            [ ValidateUserPlaylists.NoIncludedPlaylists
              ValidateUserPlaylists.NoTargetPlaylists ]
            |> ValidateUserPlaylists.Errors
          | [], _ -> [ ValidateUserPlaylists.NoIncludedPlaylists ] |> ValidateUserPlaylists.Errors
          | _, [] -> [ ValidateUserPlaylists.NoTargetPlaylists ] |> ValidateUserPlaylists.Errors
          | _, _ -> ValidateUserPlaylists.Ok
      }

[<RequireQualifiedAccess>]
module UserSettings =
  type Load = UserId -> Task<UserSettings.UserSettings>

  type Update = UserId -> UserSettings.UserSettings -> Task

  let setPlaylistSize (loadUserSettings: Load) (updateUserSettings: Update) : UserSettings.SetPlaylistSize =
    fun userId playlistSize ->
      task {
        let! userSettings = loadUserSettings userId

        let updatedSettings = { userSettings with PlaylistSize = playlistSize }

        do! updateUserSettings userId updatedSettings
      }

  let setLikedTracksHandling (loadUserSettings: Load) (updateInStorage: Update) : UserSettings.SetLikedTracksHandling =
    fun userId likedTracksHandling ->
      task {
        let! userSettings = loadUserSettings userId

        let updatedSettings =
          { userSettings with LikedTracksHandling = likedTracksHandling }

        do! updateInStorage userId updatedSettings
      }

[<RequireQualifiedAccess>]
module Playlist =
  type ListTracks = string -> Async<string list>

[<RequireQualifiedAccess>]
module User =
  type ListLikedTracks = Async<string list>