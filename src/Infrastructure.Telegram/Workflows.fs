﻿[<RequireQualifiedAccess>]
module Infrastructure.Telegram.Workflows

open Resources
open System
open System.Text.RegularExpressions
open Azure.Storage.Queues
open Domain.Core
open Domain.Workflows
open Infrastructure.Core
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums
open Telegram.Bot.Types.ReplyMarkups
open Telegram.Constants
open Telegram.Core
open Telegram.Workflows
open Infrastructure.Telegram.Helpers
open otsom.fs.Extensions
open otsom.fs.Telegram.Bot.Core

let escapeMarkdownString (str: string) =
  Regex.Replace(str, "([\(\)`\.#\-!+])", "\$1")

let sendButtons (bot: ITelegramBotClient) userId : SendButtons =
  fun text buttons ->
    let replyMarkup =
      buttons
      |> Seq.map (Seq.map InlineKeyboardButton.WithCallbackData)
      |> InlineKeyboardMarkup

    bot.SendTextMessageAsync(
      (userId |> UserId.value |> ChatId),
      text |> escapeMarkdownString,
      parseMode = ParseMode.MarkdownV2,
      replyMarkup = replyMarkup
    )
    |> Task.map ignore

let sendKeyboard (bot: ITelegramBotClient) userId : SendKeyboard =
  fun text buttons ->
    let replyMarkup =
      buttons |> Seq.map Seq.toArray |> Seq.toArray |> ReplyKeyboardMarkup.op_Implicit

    bot.SendTextMessageAsync(
      (userId |> UserId.value |> ChatId),
      text |> escapeMarkdownString,
      parseMode = ParseMode.MarkdownV2,
      replyMarkup = replyMarkup
    )
    |> Task.map ignore

let editMessage (bot: ITelegramBotClient) messageId userId : EditMessage =
  fun text buttons ->
    let replyMarkup =
      buttons
      |> Seq.map (Seq.map InlineKeyboardButton.WithCallbackData)
      |> InlineKeyboardMarkup

    bot.EditMessageTextAsync(
      (userId |> UserId.value |> ChatId),
      messageId,
      text |> escapeMarkdownString,
      ParseMode.MarkdownV2,
      replyMarkup = replyMarkup
    )
    |> Task.map ignore

let askForReply (bot: ITelegramBotClient) userId messageId : AskForReply =
  fun text ->
    bot.SendTextMessageAsync(
      (userId |> UserId.value |> ChatId),
      text |> escapeMarkdownString,
      parseMode = ParseMode.MarkdownV2,
      replyToMessageId = messageId,
      replyMarkup = ForceReplyMarkup()
    )
    |> Task.map ignore

let answerCallbackQuery (bot: ITelegramBotClient) callbackQueryId : AnswerCallbackQuery =
  fun text ->
    task {
      do! bot.AnswerCallbackQueryAsync(callbackQueryId, text)

      return ()
    }

let sendLink (bot: ITelegramBotClient) userId : SendLink =
  fun text linkText link ->
    bot.SendTextMessageAsync(
      (userId |> UserId.value |> ChatId),
      text |> escapeMarkdownString,
      parseMode = ParseMode.MarkdownV2,
      replyMarkup =
        (InlineKeyboardButton(linkText, Url = link)
         |> Seq.singleton
         |> Seq.singleton
         |> InlineKeyboardMarkup)
    )
    |> Task.map ignore

let private savePlaylistSize loadUser setPlaylistSize =
  fun userId playlistSize ->
    task {
      let! currentPresetId = loadUser userId |> Task.map (fun u -> u.CurrentPresetId |> Option.get)

      do! setPlaylistSize currentPresetId playlistSize
    }

let setPlaylistSize
  (sendMessage: SendMessage)
  (sendSettingsMessage: SendSettingsMessage)
  (loadUser: User.Load)
  (setPlaylistSize: Preset.SetPlaylistSize)
  =
  fun userId size ->
    let savePlaylistSize = savePlaylistSize loadUser setPlaylistSize userId

    let onSuccess () = sendSettingsMessage userId

    let onError =
      function
      | PresetSettings.PlaylistSize.TooSmall -> sendMessage Messages.PlaylistSizeTooSmall
      | PresetSettings.PlaylistSize.TooBig -> sendMessage Messages.PlaylistSizeTooBig

    PresetSettings.PlaylistSize.tryCreate size
    |> Result.taskMap savePlaylistSize
    |> TaskResult.taskEither onSuccess onError

[<RequireQualifiedAccess>]
module Playlist =

  let includePlaylist
    (replyToMessage: ReplyToMessage)
    (loadUser: User.Load)
    (includePlaylist: Playlist.IncludePlaylist)
    : Playlist.Include =
    fun userId rawPlaylistId ->
      task {
        let! currentPresetId = loadUser userId |> Task.map (fun u -> u.CurrentPresetId |> Option.get)
        let includePlaylistResult = rawPlaylistId |> includePlaylist currentPresetId

        let onSuccess (playlist: IncludedPlaylist) =
          replyToMessage $"*{playlist.Name}* successfully included into current preset!"

        let onError =
          function
          | Playlist.IncludePlaylistError.IdParsing _ ->
            replyToMessage (String.Format(Messages.PlaylistIdCannotBeParsed, (rawPlaylistId |> RawPlaylistId.value)))
          | Playlist.IncludePlaylistError.MissingFromSpotify(Playlist.MissingFromSpotifyError id) ->
            replyToMessage (String.Format(Messages.PlaylistNotFoundInSpotify, id))

        return! includePlaylistResult |> TaskResult.taskEither onSuccess onError
      }

  let excludePlaylist
    (replyToMessage: ReplyToMessage)
    (loadUser: User.Load)
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
          | Playlist.ExcludePlaylistError.MissingFromSpotify(Playlist.MissingFromSpotifyError id) ->
            replyToMessage (String.Format(Messages.PlaylistNotFoundInSpotify, id))

        return! excludePlaylistResult |> TaskResult.taskEither onSuccess onError
      }

  let targetPlaylist (replyToMessage: ReplyToMessage) (loadUser: User.Load) (targetPlaylist: Playlist.TargetPlaylist) : Playlist.Target =
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
          | Playlist.TargetPlaylistError.MissingFromSpotify(Playlist.MissingFromSpotifyError id) ->
            replyToMessage (String.Format(Messages.PlaylistNotFoundInSpotify, id))
          | Playlist.TargetPlaylistError.AccessError _ -> replyToMessage Messages.PlaylistIsReadonly

        return! targetPlaylistResult |> TaskResult.taskEither onSuccess onError
      }

  let queueGeneration
    (queueClient: QueueClient)
    (replyToMessage: ReplyToMessage)
    (loadUser: User.Load)
    (loadPreset: Preset.Load)
    (validatePreset: Preset.Validate)
    : Playlist.QueueGeneration =
    let onSuccess () =
      replyToMessage "Your playlist generation request is queued!"

    let onError errors =
      let errorsText =
        errors
        |> Seq.map (function
          | Preset.ValidationError.NoIncludedPlaylists -> "No included playlists!"
          | Preset.ValidationError.NoTargetedPlaylists -> "No target playlists!")
        |> String.concat Environment.NewLine

      replyToMessage errorsText

    let queueGeneration (preset: Preset) =
      {| PresetId = preset.Id |} |> JSON.serialize |> queueClient.SendMessageAsync |> Task.map ignore

    loadUser
    >> Task.map (fun u -> u.CurrentPresetId |> Option.get)
    >> Task.bind loadPreset
    >> Task.map validatePreset
    >> TaskResult.taskMap queueGeneration
    >> TaskResult.taskEither onSuccess onError

  let generate sendMessage (generatePlaylist: Domain.Core.Playlist.Generate) =
    let onSuccess () = sendMessage "Playlist generated!"

    let onError =
      function
      | Playlist.GenerateError.NoIncludedTracks -> sendMessage "Your preset has 0 included tracks"
      | Playlist.GenerateError.NoPotentialTracks -> sendMessage "Playlists combination in your preset produced 0 potential tracks"

    fun presetId ->
      task {
        do! sendMessage "Generating playlist..."

        return! generatePlaylist presetId |> TaskResult.taskEither onSuccess onError
      }

let parseAction: ParseAction =
  fun (str: string) ->
    match str.Split("|") with
    | [| "p"; id; "i" |] -> PresetId id |> Action.ShowPresetInfo
    | [| "p"; id; "c" |] -> PresetId id |> Action.SetCurrentPreset
    | [| "p"; id; "rm" |] -> PresetId id |> Action.RemovePreset

    | [| "p"; id; "ip"; Int page |] -> Action.ShowIncludedPlaylists(PresetId id, (Page page))
    | [| "p"; presetId; "ip"; playlistId; "i" |] ->
      Action.ShowIncludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)
    | [| "p"; presetId; "ip"; playlistId; "e" |] ->
      Action.EnableIncludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)
    | [| "p"; presetId; "ip"; playlistId; "d" |] ->
      Action.DisableIncludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)
    | [| "p"; presetId; "ip"; playlistId; "rm" |] ->
      Action.RemoveIncludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)

    | [| "p"; id; "ep"; Int page |] -> Action.ShowExcludedPlaylists(PresetId id, (Page page))
    | [| "p"; presetId; "ep"; playlistId; "i" |] ->
      Action.ShowExcludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)
    | [| "p"; presetId; "ep"; playlistId; "e" |] ->
      Action.EnableExcludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)
    | [| "p"; presetId; "ep"; playlistId; "d" |] ->
      Action.DisableExcludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)
    | [| "p"; presetId; "ep"; playlistId; "rm" |] ->
      Action.RemoveExcludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)

    | [| "p"; id; "tp"; Int page |] -> Action.ShowTargetedPlaylists(PresetId id, (Page page))
    | [| "p"; presetId; "tp"; playlistId; "i" |] ->
      Action.ShowTargetedPlaylist(PresetId presetId, PlaylistId playlistId |> WritablePlaylistId)
    | [| "p"; presetId; "tp"; playlistId; "a" |] ->
      Action.AppendToTargetedPlaylist(PresetId presetId, PlaylistId playlistId |> WritablePlaylistId)
    | [| "p"; presetId; "tp"; playlistId; "o" |] ->
      Action.OverwriteTargetedPlaylist(PresetId presetId, PlaylistId playlistId |> WritablePlaylistId)
    | [| "p"; presetId; "tp"; playlistId; "rm" |] ->
      Action.RemoveTargetedPlaylist(PresetId presetId, PlaylistId playlistId |> WritablePlaylistId)

    | [| "p"; presetId; CallbackQueryConstants.includeLikedTracks |] -> Action.IncludeLikedTracks(PresetId presetId)
    | [| "p"; presetId; CallbackQueryConstants.excludeLikedTracks |] -> Action.ExcludeLikedTracks(PresetId presetId)
    | [| "p"; presetId; CallbackQueryConstants.ignoreLikedTracks |] -> Action.IgnoreLikedTracks(PresetId presetId)

    | [| "p"; presetId; CallbackQueryConstants.enableRecommendations |] -> Action.EnableRecommendations(PresetId presetId)
    | [| "p"; presetId; CallbackQueryConstants.disableRecommendations |] -> Action.DisableRecommendations(PresetId presetId)

    | [| "p" |] -> Action.ShowUserPresets