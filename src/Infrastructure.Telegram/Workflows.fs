[<RequireQualifiedAccess>]
module Infrastructure.Telegram.Workflows

open MusicPlatform
open Resources
open System
open Domain.Core
open Domain.Workflows
open Infrastructure.Core
open Telegram.Bot
open Telegram.Constants
open Telegram.Core
open Telegram.Workflows
open Infrastructure.Telegram.Helpers
open otsom.fs.Extensions
open otsom.fs.Telegram.Bot.Core

let answerCallbackQuery (bot: ITelegramBotClient) callbackQueryId : AnswerCallbackQuery =
  fun () ->
    task {
      do! bot.AnswerCallbackQueryAsync(callbackQueryId)

      return ()
    }

let showNotification (bot: ITelegramBotClient) callbackQueryId : ShowNotification =
  fun text ->
    task {
      do! bot.AnswerCallbackQueryAsync(callbackQueryId, text)

      return ()
    }

[<RequireQualifiedAccess>]
module Playlist =

  let excludePlaylist
    (replyToMessage: ReplyToMessage)
    (loadUser: User.Get)
    (excludePlaylist: Playlist.ExcludePlaylist)
    : Playlist.Exclude =
    fun userId rawPlaylistId ->
      task {
        let! currentPresetId = loadUser userId |> Task.map (fun u -> u.CurrentPresetId |> Option.get)

        let excludePlaylistResult = rawPlaylistId |> excludePlaylist currentPresetId

        let onSuccess (playlist: ExcludedPlaylist) =
          replyToMessage $"*{playlist.Name}* successfully excluded from current preset!"

        let onError =
          function
          | Playlist.ExcludePlaylistError.IdParsing _ ->
            replyToMessage (String.Format(Messages.PlaylistIdCannotBeParsed, (rawPlaylistId |> RawPlaylistId.value)))
          | Playlist.ExcludePlaylistError.Load(Playlist.LoadError.NotFound) ->
            let (Playlist.RawPlaylistId rawPlaylistId) = rawPlaylistId
            replyToMessage (String.Format(Messages.PlaylistNotFoundInSpotify, rawPlaylistId))

        return! excludePlaylistResult |> TaskResult.taskEither onSuccess onError |> Task.ignore
      }

  let targetPlaylist (replyToMessage: ReplyToMessage) (loadUser: User.Get) (targetPlaylist: Playlist.TargetPlaylist) : Playlist.Target =
    fun userId rawPlaylistId ->
      task {
        let! currentPresetId = loadUser userId |> Task.map (fun u -> u.CurrentPresetId |> Option.get)

        let targetPlaylistResult = rawPlaylistId |> targetPlaylist currentPresetId

        let onSuccess (playlist: TargetedPlaylist) =
          replyToMessage $"*{playlist.Name}* successfully targeted for current preset!"

        let onError =
          function
          | Playlist.TargetPlaylistError.IdParsing _ ->
            replyToMessage (String.Format(Messages.PlaylistIdCannotBeParsed, (rawPlaylistId |> RawPlaylistId.value)))
          | Playlist.TargetPlaylistError.Load(Playlist.LoadError.NotFound) ->
            let (Playlist.RawPlaylistId rawPlaylistId) = rawPlaylistId
            replyToMessage (String.Format(Messages.PlaylistNotFoundInSpotify, rawPlaylistId))
          | Playlist.TargetPlaylistError.AccessError _ -> replyToMessage Messages.PlaylistIsReadonly

        return! targetPlaylistResult |> TaskResult.taskEither onSuccess onError |> Task.ignore
      }

let parseAction: ParseAction =
  fun (str: string) ->
    match str.Split("|") with
    | [| "p"; id; "i" |] -> PresetId id |> PresetActions.Show |> Action.Preset
    | [| "p"; id; "c" |] -> PresetId id |> Action.SetCurrentPreset
    | [| "p"; id; "rm" |] -> PresetId id |> Action.RemovePreset
    | [| "p"; id; "r" |] -> PresetId id |> PresetActions.Run |> Action.Preset

    | [| "p"; id; "ip"; Int page |] ->
      IncludedPlaylistActions.List(PresetId id, (Page page)) |> Action.IncludedPlaylist
    | [| "p"; presetId; "ip"; playlistId; "i" |] ->
      IncludedPlaylistActions.Show(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId) |> Action.IncludedPlaylist
    | [| "p"; presetId; "ip"; playlistId; "e" |] ->
      Action.EnableIncludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)
    | [| "p"; presetId; "ip"; playlistId; "d" |] ->
      Action.DisableIncludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)
    | [| "p"; presetId; "ip"; playlistId; "rm" |] ->
      IncludedPlaylistActions.Remove(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId) |> Action.IncludedPlaylist

    | [| "p"; id; "ep"; Int page |] ->
      ExcludedPlaylistActions.List(PresetId id, (Page page)) |> Action.ExcludedPlaylist
    | [| "p"; presetId; "ep"; playlistId; "i" |] ->
      ExcludedPlaylistActions.Show(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId) |> Action.ExcludedPlaylist
    | [| "p"; presetId; "ep"; playlistId; "e" |] ->
      Action.EnableExcludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)
    | [| "p"; presetId; "ep"; playlistId; "d" |] ->
      Action.DisableExcludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)
    | [| "p"; presetId; "ep"; playlistId; "rm" |] ->
      ExcludedPlaylistActions.Remove(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId) |> Action.ExcludedPlaylist

    | [| "p"; id; "tp"; Int page |] -> TargetedPlaylistActions.List(PresetId id, (Page page)) |> Action.TargetedPlaylist
    | [| "p"; presetId; "tp"; playlistId; "i" |] ->
      TargetedPlaylistActions.Show(PresetId presetId, PlaylistId playlistId |> WritablePlaylistId) |> Action.TargetedPlaylist
    | [| "p"; presetId; "tp"; playlistId; "a" |] ->
      Action.AppendToTargetedPlaylist(PresetId presetId, PlaylistId playlistId |> WritablePlaylistId)
    | [| "p"; presetId; "tp"; playlistId; "o" |] ->
      Action.OverwriteTargetedPlaylist(PresetId presetId, PlaylistId playlistId |> WritablePlaylistId)
    | [| "p"; presetId; "tp"; playlistId; "rm" |] ->
      TargetedPlaylistActions.Remove(PresetId presetId, PlaylistId playlistId |> WritablePlaylistId) |> Action.TargetedPlaylist

    | [| "p"; presetId; CallbackQueryConstants.includeLikedTracks |] ->
      PresetSettingsActions.IncludeLikedTracks(PresetId presetId) |> Action.PresetSettings
    | [| "p"; presetId; CallbackQueryConstants.excludeLikedTracks |] ->
      PresetSettingsActions.ExcludeLikedTracks(PresetId presetId) |> Action.PresetSettings
    | [| "p"; presetId; CallbackQueryConstants.ignoreLikedTracks |] ->
      PresetSettingsActions.IgnoreLikedTracks(PresetId presetId) |> Action.PresetSettings

    | [| "p"; presetId; CallbackQueryConstants.enableRecommendations |] ->
      PresetSettingsActions.EnableRecommendations(PresetId presetId) |> Action.PresetSettings
    | [| "p"; presetId; CallbackQueryConstants.disableRecommendations |] ->
      PresetSettingsActions.DisableRecommendations(PresetId presetId) |> Action.PresetSettings

    | [| "p"; presetId; CallbackQueryConstants.enableUniqueArtists |] ->
      PresetSettingsActions.EnableUniqueArtists(PresetId presetId) |> Action.PresetSettings
    | [| "p"; presetId; CallbackQueryConstants.disableUniqueArtists |] ->
      PresetSettingsActions.DisableUniqueArtists(PresetId presetId) |> Action.PresetSettings

    | [| "p" |] -> Action.User(UserActions.ListPresets())