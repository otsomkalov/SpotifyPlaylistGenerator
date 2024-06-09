module Telegram.Workflows

open System.Threading.Tasks
open Domain.Core
open Domain.Workflows
open Microsoft.FSharp.Core
open Resources
open Telegram.Bot.Types.ReplyMarkups
open Telegram.Constants
open Telegram.Core
open otsom.fs.Extensions
open otsom.fs.Telegram.Bot.Core

[<Literal>]
let keyboardColumns = 4

[<Literal>]
let buttonsPerPage = 20

type SendLink = string -> string -> string -> Task<unit>

let sendUserPresets (sendButtons: SendMessageButtons) (loadUser: User.Get) : SendUserPresets =
  fun userId ->
    task {
      let! user = loadUser userId

      let keyboardMarkup =
        user.Presets
        |> Seq.map (fun p -> InlineKeyboardButton.WithCallbackData(p.Name, $"p|{p.Id |> PresetId.value}|i"))
        |> Seq.singleton
        |> InlineKeyboardMarkup

      do! sendButtons "Your presets" keyboardMarkup
    }

let private getPresetMessage =
  fun (preset: Preset) ->
    task {
      let presetId = preset.Id |> PresetId.value

      let likedTracksHandlingText, likedTracksButtonText, likedTracksButtonData =
        match preset.Settings.LikedTracksHandling with
        | PresetSettings.LikedTracksHandling.Include ->
          Messages.LikedTracksIncluded, Buttons.ExcludeLikedTracks, $"p|{presetId}|{CallbackQueryConstants.excludeLikedTracks}"
        | PresetSettings.LikedTracksHandling.Exclude ->
          Messages.LikedTracksExcluded, Buttons.IgnoreLikedTracks, $"p|{presetId}|{CallbackQueryConstants.ignoreLikedTracks}"
        | PresetSettings.LikedTracksHandling.Ignore ->
          Messages.LikedTracksIgnored, Buttons.IncludeLikedTracks, $"p|{presetId}|{CallbackQueryConstants.includeLikedTracks}"

      let recommendationsText, recommendationsButtonText, recommendationsButtonData =
        match preset.Settings.RecommendationsEnabled with
        | true ->
          Messages.RecommendationsEnabled,
          Buttons.DisableRecommendations,
          sprintf "p|%s|%s" presetId CallbackQueryConstants.disableRecommendations
        | false ->
          Messages.RecommendationsDisabled,
          Buttons.EnableRecommendations,
          sprintf "p|%s|%s" presetId CallbackQueryConstants.enableRecommendations

      let uniqueArtistsText, uniqueArtistsButtonText, uniqueArtistsButtonData =
        match preset.Settings.UniqueArtists with
        | true ->
          Messages.UniqueArtistsEnabled,
          Buttons.DisableUniqueArtists,
          sprintf "p|%s|%s" presetId CallbackQueryConstants.disableUniqueArtists
        | false ->
          Messages.UniqueArtistsDisabled,
          Buttons.EnableUniqueArtists,
          sprintf "p|%s|%s" presetId CallbackQueryConstants.enableUniqueArtists

      let text =
        System.String.Format(
          Messages.PresetInfo,
          preset.Name,
          likedTracksHandlingText,
          recommendationsText,
          uniqueArtistsText,
          (preset.Settings.PlaylistSize |> PresetSettings.PlaylistSize.value)
        )

      let keyboard =
        seq {
          InlineKeyboardButton.WithCallbackData(likedTracksButtonText, likedTracksButtonData)
          InlineKeyboardButton.WithCallbackData(uniqueArtistsButtonText, uniqueArtistsButtonData)
          InlineKeyboardButton.WithCallbackData(recommendationsButtonText, recommendationsButtonData)
        }

      return (text, keyboard)
    }

let sendPresetInfo (getPreset: Preset.Get) (editMessage: EditMessageButtons) : SendPresetInfo =
  fun presetId ->
    task {
      let! preset = getPreset presetId

      let! text, keyboard = getPresetMessage preset

      let presetId = presetId |> PresetId.value

      let keyboardMarkup =
        seq {
          seq {
            InlineKeyboardButton.WithCallbackData("Included playlists", $"p|%s{presetId}|ip|0")
            InlineKeyboardButton.WithCallbackData("Excluded playlists", $"p|%s{presetId}|ep|0")
            InlineKeyboardButton.WithCallbackData("Target playlists", $"p|%s{presetId}|tp|0")
          }

          keyboard

          seq { InlineKeyboardButton.WithCallbackData("Set as current", $"p|%s{presetId}|c") }

          seq { InlineKeyboardButton.WithCallbackData("Remove", sprintf "p|%s|rm" presetId) }

          seq { InlineKeyboardButton.WithCallbackData("<< Back >>", "p") }
        }

      do! editMessage text (keyboardMarkup |> InlineKeyboardMarkup)
    }

let setCurrentPreset (answerCallbackQuery: AnswerCallbackQuery) (setCurrentPreset: User.SetCurrentPreset) : SetCurrentPreset =
  fun userId presetId ->
    task {
      do! setCurrentPreset userId presetId

      return! answerCallbackQuery "Current preset is successfully set!"
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

  let backButton = InlineKeyboardButton.WithCallbackData("<< Back >>", $"p|{presetId}|i")

  let prevButton =
    if page > 0 then
      Some(InlineKeyboardButton.WithCallbackData("<< Prev", $"p|{presetId}|ip|{page - 1}"))
    else
      None

  let nextButton =
    if remainingPlaylists.Length > buttonsPerPage then
      Some(InlineKeyboardButton.WithCallbackData("Next >>", $"p|{presetId}|ip|{page + 1}"))
    else
      None

  let serviceButtons =
    match (prevButton, nextButton) with
    | Some pb, Some nb -> [ pb; backButton; nb ]
    | None, Some nb -> [ backButton; nb ]
    | Some pb, None -> [ pb; backButton ]
    | _ -> [ backButton ]

  Seq.append playlistsButtons (serviceButtons |> Seq.ofList |> Seq.singleton) |> InlineKeyboardMarkup

[<RequireQualifiedAccess>]
module IncludedPlaylist =
  let list (getPreset: Preset.Get) (editMessageButtons: EditMessageButtons) : IncludedPlaylist.List =
    let createButtonFromPlaylist presetId =
      fun (playlist: IncludedPlaylist) ->
        InlineKeyboardButton.WithCallbackData(
          playlist.Name,
          sprintf "p|%s|ip|%s|i" (presetId |> PresetId.value) (playlist.Id |> ReadablePlaylistId.value |> PlaylistId.value)
        )

    fun presetId page ->
      let createButtonFromPlaylist = createButtonFromPlaylist presetId

      task {
        let! preset = getPreset presetId

        let replyMarkup =
          createPlaylistsPage page preset.IncludedPlaylists createButtonFromPlaylist preset.Id

        return! editMessageButtons $"Preset *{preset.Name}* has the next included playlists:" replyMarkup
      }

  let enable
    (enableIncludedPlaylist: Domain.Core.IncludedPlaylist.Enable)
    (answerCallbackQuery: AnswerCallbackQuery)
    (showIncludedPlaylist: ShowIncludedPlaylist)
    : IncludedPlaylist.Enable =
    fun presetId playlistId ->
      task {
        do! enableIncludedPlaylist presetId playlistId

        do! answerCallbackQuery "Enabled"

        return! showIncludedPlaylist presetId playlistId
      }

  let disable
    (disableIncludedPlaylist: Domain.Core.IncludedPlaylist.Disable)
    (answerCallbackQuery: AnswerCallbackQuery)
    (showIncludedPlaylist: ShowIncludedPlaylist)
    : IncludedPlaylist.Disable =
    fun presetId playlistId ->
      task {
        do! disableIncludedPlaylist presetId playlistId

        do! answerCallbackQuery "Disabled"

        return! showIncludedPlaylist presetId playlistId
      }

[<RequireQualifiedAccess>]
module ExcludedPlaylist =
  let enable
    (enableExcludedPlaylist: Domain.Core.ExcludedPlaylist.Enable)
    (answerCallbackQuery: AnswerCallbackQuery)
    (showExcludedPlaylist: ShowExcludedPlaylist)
    : ExcludedPlaylist.Enable =
    fun presetId playlistId ->
      task {
        do! enableExcludedPlaylist presetId playlistId

        do! answerCallbackQuery "Enabled"

        return! showExcludedPlaylist presetId playlistId
      }

  let disable
    (disableExcludedPlaylist: Domain.Core.ExcludedPlaylist.Disable)
    (answerCallbackQuery: AnswerCallbackQuery)
    (showExcludedPlaylist: ShowExcludedPlaylist)
    : ExcludedPlaylist.Enable =
    fun presetId playlistId ->
      task {
        do! disableExcludedPlaylist presetId playlistId

        do! answerCallbackQuery "Disabled"

        return! showExcludedPlaylist presetId playlistId
      }

let showExcludedPlaylists (getPreset: Preset.Get) (editMessageButtons: EditMessageButtons) : ShowExcludedPlaylists =
  let createButtonFromPlaylist presetId =
    fun (playlist: ExcludedPlaylist) ->
      InlineKeyboardButton.WithCallbackData(
        playlist.Name,
        sprintf "p|%s|ep|%s|i" (presetId |> PresetId.value) (playlist.Id |> ReadablePlaylistId.value |> PlaylistId.value)
      )

  fun presetId page ->
    let createButtonFromPlaylist = createButtonFromPlaylist presetId

    task {
      let! preset = getPreset presetId

      let replyMarkup =
        createPlaylistsPage page preset.ExcludedPlaylists createButtonFromPlaylist preset.Id

      return! editMessageButtons $"Preset *{preset.Name}* has the next excluded playlists:" replyMarkup
    }

let showTargetedPlaylists (getPreset: Preset.Get) (editMessageButtons: EditMessageButtons) : ShowTargetedPlaylists =
  let createButtonFromPlaylist presetId =
    fun (playlist: TargetedPlaylist) ->
      InlineKeyboardButton.WithCallbackData(
        playlist.Name,
        sprintf "p|%s|tp|%s|i" (presetId |> PresetId.value) (playlist.Id |> WritablePlaylistId.value |> PlaylistId.value)
      )

  fun presetId page ->
    let createButtonFromPlaylist = createButtonFromPlaylist presetId

    task {
      let! preset = getPreset presetId

      let replyMarkup =
        createPlaylistsPage page preset.TargetedPlaylists createButtonFromPlaylist preset.Id

      return! editMessageButtons $"Preset *{preset.Name}* has the next targeted playlists:" replyMarkup
    }

let private setLikedTracksHandling (answerCallbackQuery: AnswerCallbackQuery) setLikedTracksHandling (sendPresetInfo: SendPresetInfo) =
  fun presetId ->
    task {
      do! setLikedTracksHandling presetId

      do! answerCallbackQuery Messages.Updated

      return! sendPresetInfo presetId
    }

let includeLikedTracks answerCallbackQuery sendPresetInfo (includeLikedTracks: Preset.IncludeLikedTracks) : Preset.IncludeLikedTracks =
  setLikedTracksHandling answerCallbackQuery includeLikedTracks sendPresetInfo

let excludeLikedTracks answerCallbackQuery sendPresetInfo (excludeLikedTracks: Preset.ExcludeLikedTracks) : Preset.ExcludeLikedTracks =
  setLikedTracksHandling answerCallbackQuery excludeLikedTracks sendPresetInfo

let ignoreLikedTracks answerCallbackQuery sendPresetInfo (ignoreLikedTracks: Preset.IgnoreLikedTracks) : Preset.IgnoreLikedTracks =
  setLikedTracksHandling answerCallbackQuery ignoreLikedTracks sendPresetInfo

let enableRecommendations
  (enableRecommendations: Preset.EnableRecommendations)
  (answerCallbackQuery: AnswerCallbackQuery)
  (sendPresetInfo: SendPresetInfo)
  : Preset.EnableRecommendations =
  fun presetId ->
    task {
      do! enableRecommendations presetId

      do! answerCallbackQuery Messages.Updated

      return! sendPresetInfo presetId
    }

let disableRecommendations
  (disableRecommendations: Preset.DisableRecommendations)
  (answerCallbackQuery: AnswerCallbackQuery)
  (sendPresetInfo: SendPresetInfo)
  : Preset.DisableRecommendations =
  fun presetId ->
    task {
      do! disableRecommendations presetId

      do! answerCallbackQuery Messages.Updated

      return! sendPresetInfo presetId
    }

let enableUniqueArtists
  (enableUniqueArtists: Preset.EnableUniqueArtists)
  (answerCallbackQuery: AnswerCallbackQuery)
  (sendPresetInfo: SendPresetInfo)
  : Preset.EnableUniqueArtists =
  fun presetId ->
    task {
      do! enableUniqueArtists presetId

      do! answerCallbackQuery Messages.Updated

      return! sendPresetInfo presetId
    }

let disableUniqueArtists
  (disableUniqueArtists: Preset.DisableUniqueArtists)
  (answerCallbackQuery: AnswerCallbackQuery)
  (sendPresetInfo: SendPresetInfo)
  : Preset.DisableUniqueArtists =
  fun presetId ->
    task {
      do! disableUniqueArtists presetId

      do! answerCallbackQuery Messages.Updated

      return! sendPresetInfo presetId
    }

let sendSettingsMessage (loadUser: User.Get) (getPreset: Preset.Get) (sendKeyboard: SendKeyboard) : SendSettingsMessage =
  fun userId ->
    task {
      let! currentPresetId = loadUser userId |> Task.map (fun u -> u.CurrentPresetId |> Option.get)
      let! preset = getPreset currentPresetId
      let! text, _ = getPresetMessage preset

      let buttons =
        [| [| Buttons.SetPlaylistSize |]; [| "Back" |] |]
        |> ReplyKeyboardMarkup.op_Implicit

      return! sendKeyboard text buttons
    }

let sendCurrentPresetInfo (loadUser: User.Get) (getPreset: Preset.Get) (sendKeyboard: SendKeyboard) : SendCurrentPresetInfo =
  fun userId ->
    task {
      let! currentPresetId = loadUser userId |> Task.map _.CurrentPresetId

      return!
        match currentPresetId with
        | Some presetId ->
          task {
            let! preset = getPreset presetId
            let! text, _ = getPresetMessage preset

            let buttons =
              [| [| Buttons.GeneratePlaylist |]
                 [| Buttons.MyPresets |]
                 [| Buttons.CreatePreset |]

                 [| Buttons.IncludePlaylist; Buttons.ExcludePlaylist; Buttons.TargetPlaylist |]

                 [| Buttons.Settings |] |]
              |> ReplyKeyboardMarkup.op_Implicit

            return! sendKeyboard text buttons
          }
        | None ->
          let buttons =
            [| [| Buttons.MyPresets |]
               [| Buttons.CreatePreset |] |]
            |> ReplyKeyboardMarkup.op_Implicit

          sendKeyboard "You did not select current preset" buttons
    }

let private getPlaylistButtons presetId playlistId playlistType enabled =
  let presetId = presetId |> PresetId.value

  let buttonDataTemplate =
    sprintf "p|%s|%s|%s|%s" presetId playlistType (playlistId |> ReadablePlaylistId.value |> PlaylistId.value)

  let enableDisableButtonText, enableDisableButtonData =
    match enabled with
    | true -> "Disable", buttonDataTemplate "d"
    | false -> "Enable", buttonDataTemplate "e"

  seq {
    seq {
      InlineKeyboardButton.WithCallbackData(enableDisableButtonText, enableDisableButtonData)
      InlineKeyboardButton.WithCallbackData("Remove", buttonDataTemplate "rm")
    }

    seq { InlineKeyboardButton.WithCallbackData("<< Back >>", sprintf "p|%s|%s|%i" presetId playlistType 0) }
  }
  |> InlineKeyboardMarkup

let showIncludedPlaylist
  (editMessageButtons: EditMessageButtons)
  (getPreset: Preset.Get)
  (countPlaylistTracks: Playlist.CountTracks)
  : ShowIncludedPlaylist =
  fun presetId playlistId ->
    task {
      let! preset = getPreset presetId

      let includedPlaylist =
        preset.IncludedPlaylists |> List.find (fun p -> p.Id = playlistId)

      let! playlistTracksCount = countPlaylistTracks (playlistId |> ReadablePlaylistId.value)

      let messageText =
        sprintf "*Name:* %s\n*Tracks count:* %i" includedPlaylist.Name playlistTracksCount

      let buttons = getPlaylistButtons presetId playlistId "ip" includedPlaylist.Enabled

      return! editMessageButtons messageText buttons
    }

let showExcludedPlaylist
  (editMessageButtons: EditMessageButtons)
  (getPreset: Preset.Get)
  (countPlaylistTracks: Playlist.CountTracks)
  : ShowExcludedPlaylist =
  fun presetId playlistId ->
    task {
      let! preset = getPreset presetId

      let excludedPlaylist =
        preset.ExcludedPlaylists |> List.find (fun p -> p.Id = playlistId)

      let! playlistTracksCount = countPlaylistTracks (playlistId |> ReadablePlaylistId.value)

      let messageText =
        sprintf "*Name:* %s\n*Tracks count:* %i" excludedPlaylist.Name playlistTracksCount

      let buttons = getPlaylistButtons presetId playlistId "ep" excludedPlaylist.Enabled

      return! editMessageButtons messageText buttons
    }

let showTargetedPlaylist
  (editMessageButtons: EditMessageButtons)
  (getPreset: Preset.Get)
  (countPlaylistTracks: Playlist.CountTracks)
  : ShowTargetedPlaylist =
  fun presetId playlistId ->
    task {
      let! preset = getPreset presetId

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
          seq { InlineKeyboardButton.WithCallbackData(buttonText, buttonData) }
          seq { InlineKeyboardButton.WithCallbackData("Remove", sprintf "p|%s|tp|%s|rm" presetId' playlistId') }

          seq { InlineKeyboardButton.WithCallbackData("<< Back >>", sprintf "p|%s|tp|%i" presetId' 0) }
        }
        |> InlineKeyboardMarkup

      return! editMessageButtons messageText buttons
    }

let removeIncludedPlaylist
  (removeIncludedPlaylist: Domain.Core.IncludedPlaylist.Remove)
  (answerCallbackQuery: AnswerCallbackQuery)
  (showIncludedPlaylists: IncludedPlaylist.List)
  : IncludedPlaylist.Remove =
  fun presetId playlistId ->
    task {
      do! removeIncludedPlaylist presetId playlistId
      do! answerCallbackQuery "Included playlist successfully removed"

      return! showIncludedPlaylists presetId (Page 0)
    }

let removeExcludedPlaylist
  (removeExcludedPlaylist: Domain.Core.ExcludedPlaylist.Remove)
  (answerCallbackQuery: AnswerCallbackQuery)
  (showExcludedPlaylists: ShowExcludedPlaylists)
  : ExcludedPlaylist.Remove =
  fun presetId playlistId ->
    task {
      do! removeExcludedPlaylist presetId playlistId
      do! answerCallbackQuery "Excluded playlist successfully removed"

      return! showExcludedPlaylists presetId (Page 0)
    }

let removeTargetedPlaylist
  (removeTargetedPlaylist: Domain.Core.TargetedPlaylist.Remove)
  (answerCallbackQuery: AnswerCallbackQuery)
  (showTargetedPlaylists: ShowTargetedPlaylists)
  : TargetedPlaylist.Remove =
  fun presetId playlistId ->
    task {
      do! removeTargetedPlaylist presetId playlistId
      do! answerCallbackQuery "Target playlist successfully removed"

      return! showTargetedPlaylists presetId (Page 0)
    }

[<RequireQualifiedAccess>]
module TargetedPlaylist =

  let appendTracks
    (appendToTargetedPlaylist: TargetedPlaylist.AppendTracks)
    (answerCallbackQuery: AnswerCallbackQuery)
    (showTargetedPlaylist: ShowTargetedPlaylist)
    : TargetedPlaylist.AppendTracks =
    fun presetId playlistId ->
      task {
        do! appendToTargetedPlaylist presetId playlistId
        do! answerCallbackQuery "Target playlist will be appended with generated tracks"

        return! showTargetedPlaylist presetId playlistId
      }

  let overwritePlaylist
    (overwriteTargetedPlaylist: TargetedPlaylist.OverwriteTracks)
    (answerCallbackQuery: AnswerCallbackQuery)
    (showTargetedPlaylist: ShowTargetedPlaylist)
    : TargetedPlaylist.OverwriteTracks =
    fun presetId playlistId ->
      task {
        do! overwriteTargetedPlaylist presetId playlistId
        do! answerCallbackQuery "Target playlist will be overwritten with generated tracks"

        return! showTargetedPlaylist presetId playlistId
      }

[<RequireQualifiedAccess>]
module Message =
  let createPreset (createPreset: Preset.Create) (sendPresetInfo: SendPresetInfo) : Message.CreatePreset =
    fun name ->
      task {
        let! presetId = createPreset name

        return! sendPresetInfo presetId
      }

[<RequireQualifiedAccess>]
module User =
  let removePreset (removePreset: User.RemovePreset) (sendUserPresets: SendUserPresets) : User.RemovePreset =
    fun userId presetId ->
      task {
        do! removePreset userId presetId

        return! sendUserPresets userId
      }
