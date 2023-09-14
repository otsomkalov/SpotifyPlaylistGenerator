module Telegram.Workflows

open System.Text.RegularExpressions
open System.Threading.Tasks
open Domain.Core
open Domain.Workflows
open Resources
open Telegram.Constants
open Telegram.Core
open Telegram.Helpers

[<Literal>]
let keyboardColumns = 4

[<Literal>]
let buttonsPerPage = 20

type MessageButton = string * string
type KeyboardButton = string

type Button =
  | Message of MessageButton
  | Keyboard of KeyboardButton

type SendMessage = string -> MessageButton seq seq -> Task<unit>
type SendKeyboard = string -> KeyboardButton seq seq -> Task<unit>
type EditMessage = string -> Button seq seq -> Task<unit>

let parseAction (str: string) =
  match str with
  | _ ->
    match str.Split("|") with
    | [| "p"; Int id; "i" |] -> PresetId id |> Action.ShowPresetInfo
    | [| "p"; Int id; "c" |] -> PresetId id |> Action.SetCurrentPreset

    | [| "p"; Int id; "ip"; Int page |] -> Action.ShowIncludedPlaylists(PresetId id, (Page page))
    | [| "p"; Int presetId; "ip"; playlistId; "i" |] ->
      Action.ShowIncludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)
    | [| "p"; Int presetId; "ip"; playlistId; "e" |] ->
      Action.EnableIncludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)
    | [| "p"; Int presetId; "ip"; playlistId; "d" |] ->
      Action.DisableIncludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)
    | [| "p"; Int presetId; "ip"; playlistId; "rm" |] ->
      Action.RemoveIncludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)

    | [| "p"; Int presetId; "ep"; playlistId; "i" |] ->
      Action.ShowExcludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)
    | [| "p"; Int id; "ep"; Int page |] -> Action.ShowExcludedPlaylists(PresetId id, (Page page))
    | [| "p"; Int presetId; "ep"; playlistId; "rm" |] ->
      Action.RemoveExcludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)

    | [| "p"; Int id; "tp"; Int page |] -> Action.ShowTargetPlaylists(PresetId id, (Page page))
    | [| "p"; Int presetId; "tp"; playlistId; "i" |] ->
      Action.ShowTargetPlaylist(PresetId presetId, PlaylistId playlistId |> WritablePlaylistId)
    | [| "p"; Int presetId; "tp"; playlistId; "a" |] ->
      Action.AppendToTargetPlaylist(PresetId presetId, PlaylistId playlistId |> WritablePlaylistId)
    | [| "p"; Int presetId; "tp"; playlistId; "o" |] ->
      Action.OverwriteTargetPlaylist(PresetId presetId, PlaylistId playlistId |> WritablePlaylistId)
    | [| "p"; Int presetId; "tp"; playlistId; "rm" |] ->
      Action.RemoveTargetPlaylist(PresetId presetId, PlaylistId playlistId |> WritablePlaylistId)

    | [| "p"; Int presetId; CallbackQueryConstants.includeLikedTracks |] -> Action.IncludeLikedTracks(PresetId presetId)
    | [| "p"; Int presetId; CallbackQueryConstants.excludeLikedTracks |] -> Action.ExcludeLikedTracks(PresetId presetId)
    | [| "p"; Int presetId; CallbackQueryConstants.ignoreLikedTracks |] -> Action.IgnoreLikedTracks(PresetId presetId)

let sendUserPresets (sendMessage: SendMessage) (listPresets: User.ListPresets) : SendUserPresets =
  fun userId ->
    task {
      let! presets = listPresets userId |> Async.StartAsTask

      let keyboardMarkup =
        presets
        |> Seq.map (fun p -> MessageButton(p.Name, $"p|{p.Id |> PresetId.value}|i"))
        |> Seq.singleton

      do! sendMessage "Your presets" keyboardMarkup
    }

let escapeMarkdownString (str: string) = Regex.Replace(str, "([`\.#\-])", "\$1")

let getPresetMessage (loadPreset: Preset.Load) : GetPresetMessage =
  fun presetId ->
    task{
      let! preset = loadPreset presetId |> Async.StartAsTask

      let presetId = presetId |> PresetId.value

      let messageText, buttonText, buttonData =
        match preset.Settings.LikedTracksHandling with
        | PresetSettings.LikedTracksHandling.Include ->
          Messages.LikedTracksIncluded, Messages.ExcludeLikedTracks, $"p|{presetId}|{CallbackQueryConstants.excludeLikedTracks}"
        | PresetSettings.LikedTracksHandling.Exclude ->
          Messages.LikedTracksExcluded, Messages.IgnoreLikedTracks, $"p|{presetId}|{CallbackQueryConstants.ignoreLikedTracks}"
        | PresetSettings.LikedTracksHandling.Ignore ->
          Messages.LikedTracksIgnored, Messages.IncludeLikedTracks, $"p|{presetId}|{CallbackQueryConstants.includeLikedTracks}"

      let text =
        System.String.Format(
          Messages.PresetInfo,
          preset.Name,
          messageText,
          (preset.Settings.PlaylistSize |> PlaylistSize.value)
        )

      return (text |> escapeMarkdownString, buttonText, buttonData)
    }

let sendPresetInfo (editMessage: EditMessage) (getPresetMessage: GetPresetMessage) messageId userId : SendPresetInfo =
  fun presetId ->
    task {
      let! text, buttonText, buttonData = getPresetMessage presetId

      let presetId = presetId |> PresetId.value

      let keyboardMarkup =
        seq {
          seq {
            Button.Message("Included playlists", $"p|%i{presetId}|ip|0")
            Button.Message("Excluded playlists", $"p|%i{presetId}|ep|0")
            Button.Message("Target playlists", $"p|%i{presetId}|tp|0")
          }

          seq { Button.Message(buttonText, buttonData) }

          seq { Button.Message("Set as current", $"p|%i{presetId}|c") }
        }

      do! editMessage text keyboardMarkup
    }

let setCurrentPreset (answerCallbackQuery: AnswerCallbackQuery) (setCurrentPreset: User.SetCurrentPreset) : Core.SetCurrentPreset =
  fun userId presetId ->
    task {
      do! setCurrentPreset userId presetId

      return! answerCallbackQuery "Current playlist id successfully set!"
    }

let internal createPlaylistsPage page (playlists: 'a list) playlistToButton presetId =
  let (Page page) = page
  let remainingPlaylists = playlists[page * buttonsPerPage ..]
  let playlistsForPage = remainingPlaylists[.. buttonsPerPage - 1]

  let playlistsButtons =
    [ 0..keyboardColumns .. playlistsForPage.Length ]
    |> List.map (fun idx -> playlistsForPage |> List.skip idx |> List.takeSafe keyboardColumns)
    |> List.map (Seq.map playlistToButton)

  let presetId = presetId |> PresetId.value

  let backButton =
    Button.Message("<< Back >>", $"p|{presetId}|i")

  let prevButton =
    if page > 0 then
      Some(Button.Message("<< Prev", $"p|{presetId}|ip|{page - 1}"))
    else
      None

  let nextButton =
    if remainingPlaylists.Length > buttonsPerPage then
      Some(Button.Message("Next >>", $"p|{presetId}|ip|{page + 1}"))
    else
      None

  let serviceButtons =
    match (prevButton, nextButton) with
    | Some pb, Some nb -> [ pb; backButton; nb ]
    | None, Some nb -> [ backButton; nb ]
    | Some pb, None -> [ pb; backButton ]
    | _ -> [ backButton ]

  Seq.append playlistsButtons (serviceButtons |> Seq.ofList |> Seq.singleton)

let showIncludedPlaylists (loadPreset: Preset.Load) (editMessage: EditMessage) : ShowIncludedPlaylists =
  let createButtonFromPlaylist presetId =
    fun (playlist: IncludedPlaylist) ->
      Button.Message(
        playlist.Name,
        sprintf "p|%i|ip|%s|i" (presetId |> PresetId.value) (playlist.Id |> ReadablePlaylistId.value |> PlaylistId.value)
      )

  fun presetId page ->
    let createButtonFromPlaylist = createButtonFromPlaylist presetId

    task {
      let! preset = loadPreset presetId

      let replyMarkup =
        createPlaylistsPage page preset.IncludedPlaylists createButtonFromPlaylist preset.Id

      return! editMessage $"Preset *{preset.Name |> escapeMarkdownString}* has the next included playlists:" replyMarkup
    }

let enableIncludedPlaylist (enableIncludedPlaylist: Domain.Core.IncludedPlaylist.Enable) (answerCallbackQuery: AnswerCallbackQuery) (showIncludedPlaylist: ShowIncludedPlaylist) : EnableIncludedPlaylist =
  fun presetId playlistId ->
    task {
      do! enableIncludedPlaylist presetId playlistId

      do! answerCallbackQuery "Disabled"

      return! showIncludedPlaylist presetId playlistId
    }

let disableIncludedPlaylist (disableIncludedPlaylist: Domain.Core.IncludedPlaylist.Disable) (answerCallbackQuery: AnswerCallbackQuery) (showIncludedPlaylist: ShowIncludedPlaylist) : DisableIncludedPlaylist =
  fun presetId playlistId ->
    task {
      do! disableIncludedPlaylist presetId playlistId

      do! answerCallbackQuery "Disabled"

      return! showIncludedPlaylist presetId playlistId
    }

let showExcludedPlaylists (loadPreset: Preset.Load) (editMessage: EditMessage) : ShowExcludedPlaylists =
  let createButtonFromPlaylist presetId =
    fun (playlist: IncludedPlaylist) ->
      Button.Message(
        playlist.Name,
        sprintf "p|%i|ep|%s|i" (presetId |> PresetId.value) (playlist.Id |> ReadablePlaylistId.value |> PlaylistId.value)
      )

  fun presetId page ->
    let createButtonFromPlaylist = createButtonFromPlaylist presetId

    task {
      let! preset = loadPreset presetId

      let replyMarkup =
        createPlaylistsPage page preset.ExcludedPlaylist createButtonFromPlaylist preset.Id

      return! editMessage $"Preset *{preset.Name |> escapeMarkdownString}* has the next excluded playlists:" replyMarkup
    }

let showTargetPlaylists (loadPreset: Preset.Load) (editMessage: EditMessage) : ShowTargetPlaylists =
  let createButtonFromPlaylist presetId =
    fun (playlist: TargetPlaylist) ->
      Button.Message(
        playlist.Name,
        sprintf "p|%i|tp|%s|i" (presetId |> PresetId.value) (playlist.Id |> WritablePlaylistId.value |> PlaylistId.value)
      )

  fun presetId page ->
    let createButtonFromPlaylist = createButtonFromPlaylist presetId

    task {
      let! preset = loadPreset presetId

      let replyMarkup =
        createPlaylistsPage page preset.TargetPlaylists createButtonFromPlaylist preset.Id

      return! editMessage $"Preset *{preset.Name |> escapeMarkdownString}* has the next target playlists:" replyMarkup
    }

let setLikedTracksHandling (answerCallbackQuery: AnswerCallbackQuery) (setLikedTracksHandling: Preset.SetLikedTracksHandling) (sendPresetInfo : SendPresetInfo) : SetLikedTracksHandling =
  fun presetId likedTracksHandling ->
    task{
      do! setLikedTracksHandling presetId likedTracksHandling

      do! answerCallbackQuery Messages.Updated

      return! sendPresetInfo presetId
    }

let askForPlaylistSize (sendMessage: SendMessage) : AskForPlaylistSize =
  fun userId ->
    sendMessage Messages.SendPlaylistSize Seq.empty

let sendSettingsMessage (sendKeyboard:SendKeyboard) (getCurrentPresetId: User.GetCurrentPresetId) (getPresetMessage: GetPresetMessage) : SendSettingsMessage =
  fun userId ->
    task {
      let! currentPresetId = getCurrentPresetId userId

      let! text, _, _ = getPresetMessage currentPresetId

      let buttons =
        seq {
          seq { KeyboardButton(Messages.SetPlaylistSize) }
          seq { KeyboardButton("Back") }
        }

      return! sendKeyboard text buttons
    }

let sendCurrentPresetInfo
  (sendKeyboard: SendKeyboard)
  (getCurrentPresetId: User.GetCurrentPresetId)
  (getPresetMessage: GetPresetMessage)
  : SendCurrentPresetInfo =
  fun userId ->
    task {
      let! currentPresetId = getCurrentPresetId userId
      let! text, _, _ = getPresetMessage currentPresetId

      let buttons =
        seq {
          seq { KeyboardButton(Messages.MyPresets) }
          seq { KeyboardButton(Messages.IncludePlaylist) }
          seq { KeyboardButton(Messages.Settings) }
        }

      return! sendKeyboard text buttons
    }
let showIncludedPlaylist (editMessage: EditMessage) (loadPreset: Preset.Load) (countPlaylistTracks: Playlist.CountTracks) : ShowIncludedPlaylist =
  fun presetId playlistId ->
    task {
      let! preset = loadPreset presetId

      let includedPlaylist =
        preset.IncludedPlaylists |> List.find (fun p -> p.Id = playlistId)

      let! playlistTracksCount = countPlaylistTracks (playlistId |> ReadablePlaylistId.value)

      let messageText =
        sprintf "*Name:* %s\n*Tracks count:* %i" includedPlaylist.Name playlistTracksCount
        |> escapeMarkdownString

      let buttons =
        seq {
          seq {
            Button.Message(
              "Remove",
              sprintf "p|%i|ip|%s|rm" (presetId |> PresetId.value) (playlistId |> ReadablePlaylistId.value |> PlaylistId.value)
            )
          }

          seq { Button.Message("<< Back >>", sprintf "p|%i|ip|%i" (presetId |> PresetId.value) 0) }
        }

      return! editMessage messageText buttons
    }

let showExcludedPlaylist (editMessage: EditMessage) (loadPreset: Preset.Load) (countPlaylistTracks: Playlist.CountTracks) : ShowExcludedPlaylist =
  fun presetId playlistId ->
    task {
      let! preset = loadPreset presetId

      let excludedPlaylist =
        preset.ExcludedPlaylist |> List.find (fun p -> p.Id = playlistId)

      let! playlistTracksCount = countPlaylistTracks (playlistId |> ReadablePlaylistId.value)

      let messageText =
        sprintf "*Name:* %s\n*Tracks count:* %i" excludedPlaylist.Name playlistTracksCount
        |> escapeMarkdownString

      let replyMarkup =
        seq {
          seq {
            Button.Message(
              "Remove",
              sprintf "p|%i|ep|%s|rm" (presetId |> PresetId.value) (playlistId |> ReadablePlaylistId.value |> PlaylistId.value)
            )
          }

          seq { Button.Message("<< Back >>", sprintf "p|%i|ep|%i" (presetId |> PresetId.value) 0) }
        }

      return! editMessage messageText replyMarkup
    }

let showTargetPlaylist
  (editMessage: EditMessage)
  (loadPreset: Preset.Load)
  (countPlaylistTracks: Playlist.CountTracks)
  : ShowTargetPlaylist =
  fun presetId playlistId ->
    task {
      let! preset = loadPreset presetId

      let targetPlaylist =
        preset.TargetPlaylists |> List.find (fun p -> p.Id = playlistId)

      let! playlistTracksCount = countPlaylistTracks (playlistId |> WritablePlaylistId.value)

      let messageText =
        sprintf "*Name:* %s\n*Tracks count:* %i\n*Overwrite?:* %b" targetPlaylist.Name playlistTracksCount targetPlaylist.Overwrite
        |> escapeMarkdownString

      let presetId' = (presetId |> PresetId.value)
      let playlistId' = (playlistId |> WritablePlaylistId.value |> PlaylistId.value)

      let buttonText, buttonDataBuilder =
        if targetPlaylist.Overwrite then
          ("Append", sprintf "p|%i|tp|%s|a")
        else
          ("Overwrite", sprintf "p|%i|tp|%s|o")

      let buttonData = buttonDataBuilder presetId' playlistId'

      let buttons =
        seq {
          seq { Button.Message(buttonText, buttonData) }
          seq { Button.Message("Remove", sprintf "p|%i|tp|%s|rm" presetId' playlistId') }

          seq { Button.Message("<< Back >>", sprintf "p|%i|tp|%i" presetId' 0) }
        }

      return! editMessage messageText buttons
    }

let removeIncludedPlaylist (answerCallbackQuery: AnswerCallbackQuery) : RemoveIncludedPlaylist =
  fun presetId playlistId ->
    answerCallbackQuery "Not implemented yet"

let removeExcludedPlaylist (answerCallbackQuery: AnswerCallbackQuery) : RemoveExcludedPlaylist =
  fun presetId playlistId ->
    answerCallbackQuery "Not implemented yet"

let removeTargetPlaylist
  (removeTargetPlaylist: Domain.Core.TargetPlaylist.Remove)
  (answerCallbackQuery: AnswerCallbackQuery)
  (showTargetPlaylists: ShowTargetPlaylists)
  : RemoveTargetPlaylist =
  fun presetId playlistId ->
    task {
      do! removeTargetPlaylist presetId playlistId
      do! answerCallbackQuery "Target playlist successfully deleted"

      return! showTargetPlaylists presetId (Page 0)
    }

let appendToTargetPlaylist
  (appendToTargetPlaylist: TargetPlaylist.AppendTracks)
  (answerCallbackQuery: AnswerCallbackQuery)
  (showTargetPlaylist: ShowTargetPlaylist)
  : AppendToTargetPlaylist =
    fun presetId playlistId ->
    task {
      do! appendToTargetPlaylist presetId playlistId
      do! answerCallbackQuery "Target playlist will be appended with generated tracks"

      return! showTargetPlaylist presetId playlistId
    }

let overwriteTargetPlaylist
  (overwriteTargetPlaylist: TargetPlaylist.OverwriteTracks)
  (answerCallbackQuery: AnswerCallbackQuery)
  (showTargetPlaylist: ShowTargetPlaylist) : OverwriteTargetPlaylist=
  fun presetId playlistId ->
    task {
      do! overwriteTargetPlaylist presetId playlistId
      do! answerCallbackQuery "Target playlist will be overwritten with generated tracks"

      return! showTargetPlaylist presetId playlistId
    }