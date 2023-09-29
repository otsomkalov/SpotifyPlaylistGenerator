module Telegram.Workflows

open System.Threading.Tasks
open Domain.Core
open Domain.Workflows
open Resources
open Telegram.Constants
open Telegram.Core
open Telegram.Helpers
open Domain.Extensions

[<Literal>]
let keyboardColumns = 4

[<Literal>]
let buttonsPerPage = 20

type MessageButton = string * string
type KeyboardButton = string

type SendMessage = string -> Task<unit>
type SendButtons = string -> MessageButton seq seq -> Task<unit>
type ReplyToMessage = string -> Task<unit>
type SendKeyboard = string -> KeyboardButton seq seq -> Task<unit>
type EditMessage = string -> MessageButton seq seq -> Task<unit>
type AskForReply = string -> Task<unit>

let parseAction (str: string) =
  match str with
  | _ ->
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

    | [| "p"; presetId; "ep"; playlistId; "i" |] ->
      Action.ShowExcludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)
    | [| "p"; id; "ep"; Int page |] -> Action.ShowExcludedPlaylists(PresetId id, (Page page))
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

    | [|"p"|] -> Action.ShowUserPresets

let sendUserPresets (sendButtons: SendButtons) (loadUser: User.Load) : SendUserPresets =
  fun userId ->
    task {
      let! user = loadUser userId

      let keyboardMarkup =
        user.Presets
        |> Seq.map (fun p -> MessageButton(p.Name, $"p|{p.Id |> PresetId.value}|i"))
        |> Seq.singleton

      do! sendButtons "Your presets" keyboardMarkup
    }

let getPresetMessage (loadPreset: Preset.Load) : GetPresetMessage =
  fun presetId ->
    task{
      let! preset = loadPreset presetId

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

      return (text, buttonText, buttonData)
    }

let sendPresetInfo (editMessage: EditMessage) (getPresetMessage: GetPresetMessage) : SendPresetInfo =
  fun presetId ->
    task {
      let! text, buttonText, buttonData = getPresetMessage presetId

      let presetId = presetId |> PresetId.value

      let keyboardMarkup =
        seq {
          seq {
            MessageButton("Included playlists", $"p|%s{presetId}|ip|0")
            MessageButton("Excluded playlists", $"p|%s{presetId}|ep|0")
            MessageButton("Target playlists", $"p|%s{presetId}|tp|0")
          }

          seq { MessageButton(buttonText, buttonData) }

          seq { MessageButton("Set as current", $"p|%s{presetId}|c") }

          seq { MessageButton("Remove", sprintf "p|%s|rm" presetId) }

          seq { MessageButton("<< Back >>", "p")}
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
    MessageButton("<< Back >>", $"p|{presetId}|i")

  let prevButton =
    if page > 0 then
      Some(MessageButton("<< Prev", $"p|{presetId}|ip|{page - 1}"))
    else
      None

  let nextButton =
    if remainingPlaylists.Length > buttonsPerPage then
      Some(MessageButton("Next >>", $"p|{presetId}|ip|{page + 1}"))
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
      MessageButton(
        playlist.Name,
        sprintf "p|%s|ip|%s|i" (presetId |> PresetId.value) (playlist.Id |> ReadablePlaylistId.value |> PlaylistId.value)
      )

  fun presetId page ->
    let createButtonFromPlaylist = createButtonFromPlaylist presetId

    task {
      let! preset = loadPreset presetId

      let replyMarkup =
        createPlaylistsPage page preset.IncludedPlaylists createButtonFromPlaylist preset.Id

      return! editMessage $"Preset *{preset.Name}* has the next included playlists:" replyMarkup
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
    fun (playlist: ExcludedPlaylist) ->
      MessageButton(
        playlist.Name,
        sprintf "p|%s|ep|%s|i" (presetId |> PresetId.value) (playlist.Id |> ReadablePlaylistId.value |> PlaylistId.value)
      )

  fun presetId page ->
    let createButtonFromPlaylist = createButtonFromPlaylist presetId

    task {
      let! preset = loadPreset presetId

      let replyMarkup =
        createPlaylistsPage page preset.ExcludedPlaylist createButtonFromPlaylist preset.Id

      return! editMessage $"Preset *{preset.Name}* has the next excluded playlists:" replyMarkup
    }

let showTargetedPlaylists (loadPreset: Preset.Load) (editMessage: EditMessage) : ShowTargetedPlaylists =
  let createButtonFromPlaylist presetId =
    fun (playlist: TargetedPlaylist) ->
      MessageButton(
        playlist.Name,
        sprintf "p|%s|tp|%s|i" (presetId |> PresetId.value) (playlist.Id |> WritablePlaylistId.value |> PlaylistId.value)
      )

  fun presetId page ->
    let createButtonFromPlaylist = createButtonFromPlaylist presetId

    task {
      let! preset = loadPreset presetId

      let replyMarkup =
        createPlaylistsPage page preset.TargetedPlaylists createButtonFromPlaylist preset.Id

      return! editMessage $"Preset *{preset.Name}* has the next targeted playlists:" replyMarkup
    }

let setLikedTracksHandling (answerCallbackQuery: AnswerCallbackQuery) (setLikedTracksHandling: Preset.SetLikedTracksHandling) (sendPresetInfo : SendPresetInfo) : SetLikedTracksHandling =
  fun presetId likedTracksHandling ->
    task{
      do! setLikedTracksHandling presetId likedTracksHandling

      do! answerCallbackQuery Messages.Updated

      return! sendPresetInfo presetId
    }

let sendSettingsMessage (loadUser: User.Load) (getPresetMessage: GetPresetMessage) (sendKeyboard:SendKeyboard) : SendSettingsMessage =
  fun userId ->
    task {
      let! currentPresetId = loadUser userId |> Task.map (fun u -> u.CurrentPresetId |> Option.get)

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
  (loadUser: User.Load)
  (getPresetMessage: GetPresetMessage)
  : SendCurrentPresetInfo =
  fun userId ->
    task {
      let! currentPresetId = loadUser userId |> Task.map (fun u -> u.CurrentPresetId)

      return!
        match currentPresetId with
        | Some presetId ->
          task{
            let! text, _, _ = getPresetMessage presetId

            let buttons =
              seq {
                seq { KeyboardButton(Messages.MyPresets) }
                seq { KeyboardButton(Messages.CreatePreset) }
                seq { KeyboardButton(Messages.IncludePlaylist) }
                seq { KeyboardButton(Messages.Settings) }
              }

            return! sendKeyboard text buttons
          }
        | None ->
          let buttons =
              seq {
                seq { KeyboardButton(Messages.MyPresets) }
                seq { KeyboardButton(Messages.CreatePreset) }
              }

          sendKeyboard "You did not select current preset" buttons
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

      let buttons =
        seq {
          seq {
            MessageButton(
              "Remove",
              sprintf "p|%s|ip|%s|rm" (presetId |> PresetId.value) (playlistId |> ReadablePlaylistId.value |> PlaylistId.value)
            )
          }

          seq { MessageButton("<< Back >>", sprintf "p|%s|ip|%i" (presetId |> PresetId.value) 0) }
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

      let replyMarkup =
        seq {
          seq {
            MessageButton(
              "Remove",
              sprintf "p|%s|ep|%s|rm" (presetId |> PresetId.value) (playlistId |> ReadablePlaylistId.value |> PlaylistId.value)
            )
          }

          seq { MessageButton("<< Back >>", sprintf "p|%s|ep|%i" (presetId |> PresetId.value) 0) }
        }

      return! editMessage messageText replyMarkup
    }

let showTargetedPlaylist
  (editMessage: EditMessage)
  (loadPreset: Preset.Load)
  (countPlaylistTracks: Playlist.CountTracks)
  : ShowTargetedPlaylist =
  fun presetId playlistId ->
    task {
      let! preset = loadPreset presetId

      let targetPlaylist =
        preset.TargetedPlaylists |> List.find (fun p -> p.Id = playlistId)

      let! playlistTracksCount = countPlaylistTracks (playlistId |> WritablePlaylistId.value)

      let messageText =
        sprintf "*Name:* %s\n*Tracks count:* %i\n*Overwrite?:* %b" targetPlaylist.Name playlistTracksCount targetPlaylist.Overwrite

      let presetId' = (presetId |> PresetId.value)
      let playlistId' = (playlistId |> WritablePlaylistId.value |> PlaylistId.value)

      let buttonText, buttonDataBuilder =
        if targetPlaylist.Overwrite then
          ("Append", sprintf "p|%s|tp|%s|a")
        else
          ("Overwrite", sprintf "p|%s|tp|%s|o")

      let buttonData = buttonDataBuilder presetId' playlistId'

      let buttons =
        seq {
          seq { MessageButton(buttonText, buttonData) }
          seq { MessageButton("Remove", sprintf "p|%s|tp|%s|rm" presetId' playlistId') }

          seq { MessageButton("<< Back >>", sprintf "p|%s|tp|%i" presetId' 0) }
        }

      return! editMessage messageText buttons
    }

let removeIncludedPlaylist (answerCallbackQuery: AnswerCallbackQuery) : RemoveIncludedPlaylist =
  fun presetId playlistId ->
    answerCallbackQuery "Not implemented yet"

let removeExcludedPlaylist (answerCallbackQuery: AnswerCallbackQuery) : RemoveExcludedPlaylist =
  fun presetId playlistId ->
    answerCallbackQuery "Not implemented yet"

let removeTargetedPlaylist
  (removeTargetedPlaylist: Domain.Core.TargetedPlaylist.Remove)
  (answerCallbackQuery: AnswerCallbackQuery)
  (showTargetedPlaylists: ShowTargetedPlaylists)
  : RemoveTargetedPlaylist =
  fun presetId playlistId ->
    task {
      do! removeTargetedPlaylist presetId playlistId
      do! answerCallbackQuery "Target playlist successfully deleted"

      return! showTargetedPlaylists presetId (Page 0)
    }

let appendToTargetedPlaylist
  (appendToTargetedPlaylist: TargetedPlaylist.AppendTracks)
  (answerCallbackQuery: AnswerCallbackQuery)
  (showTargetedPlaylist: ShowTargetedPlaylist)
  : AppendToTargetedPlaylist =
    fun presetId playlistId ->
    task {
      do! appendToTargetedPlaylist presetId playlistId
      do! answerCallbackQuery "Target playlist will be appended with generated tracks"

      return! showTargetedPlaylist presetId playlistId
    }

let overwriteTargetedPlaylist
  (overwriteTargetedPlaylist: TargetedPlaylist.OverwriteTracks)
  (answerCallbackQuery: AnswerCallbackQuery)
  (showTargetedPlaylist: ShowTargetedPlaylist) : OverwriteTargetedPlaylist=
  fun presetId playlistId ->
    task {
      do! overwriteTargetedPlaylist presetId playlistId
      do! answerCallbackQuery "Target playlist will be overwritten with generated tracks"

      return! showTargetedPlaylist presetId playlistId
    }

[<RequireQualifiedAccess>]
module Message =
  let createPreset (createPreset : Preset.Create) (sendPresetInfo: SendPresetInfo) : Message.CreatePreset =
    fun name ->
      task{
        let! presetId = createPreset name

        return! sendPresetInfo presetId
      }

[<RequireQualifiedAccess>]
module CallbackQuery =
  let removePreset (removePreset: Preset.Remove) (sendUserPresets: SendUserPresets) : CallbackQuery.RemovePreset =
    fun presetId ->
      task{
        let! removedPreset = removePreset presetId

        return! sendUserPresets removedPreset.UserId
      }