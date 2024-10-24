﻿module Telegram.Workflows

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
open System

[<Literal>]
let keyboardColumns = 4

[<Literal>]
let buttonsPerPage = 20

type SendLink = string -> string -> string -> Task<unit>

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
        String.Format(
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

let private getPlaylistButtons presetId playlistId playlistType enabled specificButtons =
  let presetId = presetId |> PresetId.value

  let buttonDataTemplate =
    sprintf "p|%s|%s|%s|%s" presetId playlistType (playlistId |> PlaylistId.value)

  let enableDisableButtonText, enableDisableButtonData =
    match enabled with
    | true -> "Disable", buttonDataTemplate "d"
    | false -> "Enable", buttonDataTemplate "e"

  seq {
    yield specificButtons

    yield seq {
      InlineKeyboardButton.WithCallbackData(enableDisableButtonText, enableDisableButtonData)
      InlineKeyboardButton.WithCallbackData("Remove", buttonDataTemplate "rm")
    }

    yield seq { InlineKeyboardButton.WithCallbackData("<< Back >>", sprintf "p|%s|%s|%i" presetId playlistType 0) }
  }
  |> InlineKeyboardMarkup

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

  let show
    (editMessageButtons: EditMessageButtons)
    (getPreset: Preset.Get)
    (countPlaylistTracks: Playlist.CountTracks)
    : IncludedPlaylist.Show =
    fun presetId playlistId ->
      task {
        let! preset = getPreset presetId

        let includedPlaylist =
          preset.IncludedPlaylists |> List.find (fun p -> p.Id = playlistId)

        let! playlistTracksCount = countPlaylistTracks (playlistId |> ReadablePlaylistId.value)

        let messageText =
          sprintf "*Name:* %s\n*Tracks count:* %i" includedPlaylist.Name playlistTracksCount

        let buttons = getPlaylistButtons presetId (playlistId |> ReadablePlaylistId.value) "ip" includedPlaylist.Enabled Seq.empty

        return! editMessageButtons messageText buttons
      }

  let enable
    (enableIncludedPlaylist: Domain.Core.IncludedPlaylist.Enable)
    (showNotification: ShowNotification)
    (showIncludedPlaylist: IncludedPlaylist.Show)
    : IncludedPlaylist.Enable =
    fun presetId playlistId ->
      task {
        do! enableIncludedPlaylist presetId playlistId

        do! showNotification "Enabled"

        return! showIncludedPlaylist presetId playlistId
      }

  let disable
    (disableIncludedPlaylist: Domain.Core.IncludedPlaylist.Disable)
    (showNotification: ShowNotification)
    (showIncludedPlaylist: IncludedPlaylist.Show)
    : IncludedPlaylist.Disable =
    fun presetId playlistId ->
      task {
        do! disableIncludedPlaylist presetId playlistId

        do! showNotification "Disabled"

        return! showIncludedPlaylist presetId playlistId
      }

  let remove
    (removeIncludedPlaylist: Domain.Core.IncludedPlaylist.Remove)
    (showNotification: ShowNotification)
    (showIncludedPlaylists: IncludedPlaylist.List)
    : IncludedPlaylist.Remove =
    fun presetId playlistId ->
      task {
        do! removeIncludedPlaylist presetId playlistId
        do! showNotification "Included playlist successfully removed"

        return! showIncludedPlaylists presetId (Page 0)
      }

[<RequireQualifiedAccess>]
module ExcludedPlaylist =
  let list (getPreset: Preset.Get) (editMessageButtons: EditMessageButtons) : ExcludedPlaylist.List =
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

  let show
    (editMessageButtons: EditMessageButtons)
    (getPreset: Preset.Get)
    (countPlaylistTracks: Playlist.CountTracks)
    : ExcludedPlaylist.Show =
    fun presetId playlistId ->
      task {
        let! preset = getPreset presetId

        let excludedPlaylist =
          preset.ExcludedPlaylists |> List.find (fun p -> p.Id = playlistId)

        let! playlistTracksCount = countPlaylistTracks (playlistId |> ReadablePlaylistId.value)

        let messageText =
          sprintf "*Name:* %s\n*Tracks count:* %i" excludedPlaylist.Name playlistTracksCount

        let buttons = getPlaylistButtons presetId (playlistId |> ReadablePlaylistId.value) "ep" excludedPlaylist.Enabled Seq.empty

        return! editMessageButtons messageText buttons
      }

  let enable
    (enableExcludedPlaylist: Domain.Core.ExcludedPlaylist.Enable)
    (showNotification: ShowNotification)
    (showExcludedPlaylist: ExcludedPlaylist.Show)
    : ExcludedPlaylist.Enable =
    fun presetId playlistId ->
      task {
        do! enableExcludedPlaylist presetId playlistId

        do! showNotification "Enabled"

        return! showExcludedPlaylist presetId playlistId
      }

  let disable
    (disableExcludedPlaylist: Domain.Core.ExcludedPlaylist.Disable)
    (showNotification: ShowNotification)
    (showExcludedPlaylist: ExcludedPlaylist.Show)
    : ExcludedPlaylist.Enable =
    fun presetId playlistId ->
      task {
        do! disableExcludedPlaylist presetId playlistId

        do! showNotification "Disabled"

        return! showExcludedPlaylist presetId playlistId
      }

  let remove
    (removeExcludedPlaylist: Domain.Core.ExcludedPlaylist.Remove)
    (showNotification: ShowNotification)
    (listExcludedPlaylists: ExcludedPlaylist.List)
    : ExcludedPlaylist.Remove =
    fun presetId playlistId ->
      task {
        do! removeExcludedPlaylist presetId playlistId
        do! showNotification "Excluded playlist successfully removed"

        return! listExcludedPlaylists presetId (Page 0)
      }

[<RequireQualifiedAccess>]
module TargetedPlaylist =
  let list (getPreset: Preset.Get) (editMessageButtons: EditMessageButtons) : TargetedPlaylist.List =
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

  let show
    (editMessageButtons: EditMessageButtons)
    (getPreset: Preset.Get)
    (countPlaylistTracks: Playlist.CountTracks)
    : TargetedPlaylist.Show =
    fun presetId playlistId ->
      task {
        let! preset = getPreset presetId

        let targetPlaylist =
          preset.TargetedPlaylists |> List.find (fun p -> p.Id = playlistId)

        let! playlistTracksCount = countPlaylistTracks (playlistId |> WritablePlaylistId.value)

        let buttonText, buttonDataBuilder =
          if targetPlaylist.Overwrite then
            ("Append", sprintf "p|%s|tp|%s|a")
          else
            ("Overwrite", sprintf "p|%s|tp|%s|o")

        let presetId' = (presetId |> PresetId.value)
        let playlistId' = (playlistId |> WritablePlaylistId.value |> PlaylistId.value)

        let buttonData = buttonDataBuilder presetId' playlistId'

        let additionalButtons = Seq.singleton (InlineKeyboardButton.WithCallbackData(buttonText, buttonData))

        let buttons = getPlaylistButtons presetId (playlistId |> WritablePlaylistId.value) "tp" targetPlaylist.Enabled additionalButtons

        let messageText =
          sprintf "*Name:* %s\n*Tracks count:* %i\n*Overwrite?:* %b" targetPlaylist.Name playlistTracksCount targetPlaylist.Overwrite

        return! editMessageButtons messageText buttons
      }

  let appendTracks
    (appendToTargetedPlaylist: TargetedPlaylist.AppendTracks)
    (showNotification: ShowNotification)
    (showTargetedPlaylist: TargetedPlaylist.Show)
    : TargetedPlaylist.AppendTracks =
    fun presetId playlistId ->
      task {
        do! appendToTargetedPlaylist presetId playlistId
        do! showNotification "Target playlist will be appended with generated tracks"

        return! showTargetedPlaylist presetId playlistId
      }

  let overwritePlaylist
    (overwriteTargetedPlaylist: TargetedPlaylist.OverwriteTracks)
    (showNotification: ShowNotification)
    (showTargetedPlaylist: TargetedPlaylist.Show)
    : TargetedPlaylist.OverwriteTracks =
    fun presetId playlistId ->
      task {
        do! overwriteTargetedPlaylist presetId playlistId
        do! showNotification "Target playlist will be overwritten with generated tracks"

        return! showTargetedPlaylist presetId playlistId
      }

  let remove
    (removeTargetedPlaylist: Domain.Core.TargetedPlaylist.Remove)
    (showNotification: ShowNotification)
    (showTargetedPlaylists: TargetedPlaylist.List)
    : TargetedPlaylist.Remove =
    fun presetId playlistId ->
      task {
        do! removeTargetedPlaylist presetId playlistId
        do! showNotification "Target playlist successfully removed"

        return! showTargetedPlaylists presetId (Page 0)
      }

[<RequireQualifiedAccess>]
module Message =
  let createPreset (createPreset: Preset.Create) (sendPresetInfo: Preset.Show) : Message.CreatePreset =
    fun name ->
      task {
        let! presetId = createPreset name

        return! sendPresetInfo presetId
      }

[<RequireQualifiedAccess>]
module Preset =
  let show (getPreset: Preset.Get) (editMessage: EditMessageButtons) : Preset.Show =
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

            seq { InlineKeyboardButton.WithCallbackData("Run", $"p|%s{presetId}|r") }

            seq { InlineKeyboardButton.WithCallbackData("Set as current", $"p|%s{presetId}|c") }

            seq { InlineKeyboardButton.WithCallbackData("Remove", sprintf "p|%s|rm" presetId) }

            seq { InlineKeyboardButton.WithCallbackData("<< Back >>", "p") }
          }

        do! editMessage text (keyboardMarkup |> InlineKeyboardMarkup)
      }

  let queueRun
    (queueRun': Domain.Core.Preset.QueueRun)
    (sendMessage: SendMessage)
    (answerCallbackQuery: AnswerCallbackQuery)
    : Preset.Run =
    let onSuccess (preset: Preset) =
      sendMessage $"Preset *{preset.Name}* run is queued!"
      |> Task.ignore

    let onError errors =
      let errorsText =
        errors
        |> Seq.map (function
          | Preset.ValidationError.NoIncludedPlaylists -> "No included playlists!"
          | Preset.ValidationError.NoTargetedPlaylists -> "No targeted playlists!")
        |> String.concat Environment.NewLine

      sendMessage errorsText
      |> Task.ignore
      |> Task.taskTap answerCallbackQuery

    queueRun'
    >> TaskResult.taskEither onSuccess onError

  let run (sendMessage: SendMessage) (editBotMessage: BotMessageId -> string -> Task<unit>) (runPreset: Domain.Core.Preset.Run) : Preset.Run =
    fun presetId ->
      let onSuccess editMessage =
        fun preset -> editMessage $"Preset *{preset.Name}* executed!"

      let onError editMessage =
        function
        | Preset.RunError.NoIncludedTracks -> editMessage "Your preset has 0 included tracks"
        | Preset.RunError.NoPotentialTracks -> editMessage "Playlists combination in your preset produced 0 potential tracks"

      task {
        let! sentMessageId = sendMessage "Running preset..."

        let editMessage = editBotMessage sentMessageId

        return! runPreset presetId |> TaskResult.taskEither (onSuccess editMessage) (onError editMessage)
      }

[<RequireQualifiedAccess>]
module User =
  let listPresets (sendButtons: SendMessageButtons) (loadUser: User.Get) : User.ListPresets =
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

  let sendCurrentPreset (loadUser: User.Get) (getPreset: Preset.Get) (sendKeyboard: SendKeyboard) : User.SendCurrentPreset =
    loadUser
    >> Task.map _.CurrentPresetId
    >> Task.bind (function
      | Some presetId ->
        task {
          let! preset = getPreset presetId
          let! text, _ = getPresetMessage preset

          let buttons =
            [| [| Buttons.RunPreset |]
               [| Buttons.MyPresets |]
               [| Buttons.CreatePreset |]

               [| Buttons.IncludePlaylist; Buttons.ExcludePlaylist; Buttons.TargetPlaylist |]

               [| Buttons.Settings |] |]
            |> ReplyKeyboardMarkup.op_Implicit

          return! sendKeyboard text buttons
        }
      | None ->
        let buttons =
          [| [| Buttons.MyPresets |]; [| Buttons.CreatePreset |] |]
          |> ReplyKeyboardMarkup.op_Implicit

        sendKeyboard "You did not select current preset" buttons)

  let sendCurrentPresetSettings (loadUser: User.Get) (getPreset: Preset.Get) (sendKeyboard: SendKeyboard) : User.SendCurrentPresetSettings =
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

  let removePreset (removePreset: User.RemovePreset) (sendUserPresets: User.ListPresets) : User.RemovePreset =
    fun userId presetId ->
      task {
        do! removePreset userId presetId

        return! sendUserPresets userId
      }

  let setCurrentPresetSize
    (sendMessage: SendMessage)
    (sendSettingsMessage: User.SendCurrentPresetSettings)
    (setPlaylistSize: Domain.Core.User.SetCurrentPresetSize)
    : User.SetCurrentPresetSize
    =
    fun userId size ->
      let onSuccess () = sendSettingsMessage userId

      let onError =
        function
        | PresetSettings.PlaylistSize.TooSmall -> sendMessage Messages.PlaylistSizeTooSmall
        | PresetSettings.PlaylistSize.TooBig -> sendMessage Messages.PlaylistSizeTooBig
        | PresetSettings.PlaylistSize.NotANumber -> sendMessage Messages.PlaylistSizeNotANumber

      setPlaylistSize userId size
      |> TaskResult.taskEither onSuccess (onError >> Task.ignore)

  let setCurrentPreset (showNotification: ShowNotification) (setCurrentPreset: Domain.Core.User.SetCurrentPreset) : User.SetCurrentPreset =
    fun userId presetId ->
      task {
        do! setCurrentPreset userId presetId

        return! showNotification "Current preset is successfully set!"
      }

  let queueCurrentPresetRun
    (queueRun: Domain.Core.Preset.QueueRun)
    (replyToMessage: ReplyToMessage)
    (loadUser: User.Get)
    (answerCallbackQuery: AnswerCallbackQuery)
    : User.QueueCurrentPresetRun =
    let queueRun = Preset.queueRun queueRun replyToMessage answerCallbackQuery

    fun userId ->
      userId
      |> loadUser
      |> Task.map (fun u -> u.CurrentPresetId |> Option.get)
      |> Task.bind queueRun

[<RequireQualifiedAccess>]
module PresetSettings =
  let enableUniqueArtists
    (enableUniqueArtists: PresetSettings.EnableUniqueArtists)
    (showNotification: ShowNotification)
    (sendPresetInfo: Preset.Show)
    : PresetSettings.EnableUniqueArtists =
    fun presetId ->
      task {
        do! enableUniqueArtists presetId

        do! showNotification Messages.Updated

        return! sendPresetInfo presetId
      }

  let disableUniqueArtists
    (disableUniqueArtists: PresetSettings.DisableUniqueArtists)
    (showNotification: ShowNotification)
    (sendPresetInfo: Preset.Show)
    : PresetSettings.DisableUniqueArtists =
    fun presetId ->
      task {
        do! disableUniqueArtists presetId

        do! showNotification Messages.Updated

        return! sendPresetInfo presetId
      }

  let enableRecommendations
    (enableRecommendations: PresetSettings.EnableRecommendations)
    (showNotification: ShowNotification)
    (showPreset: Preset.Show)
    : PresetSettings.EnableRecommendations =
    fun presetId ->
      task {
        do! enableRecommendations presetId

        do! showNotification Messages.Updated

        return! showPreset presetId
      }

  let disableRecommendations
    (disableRecommendations: PresetSettings.DisableRecommendations)
    (showNotification: ShowNotification)
    (showPreset: Preset.Show)
    : PresetSettings.DisableRecommendations =
    fun presetId ->
      task {
        do! disableRecommendations presetId

        do! showNotification Messages.Updated

        return! showPreset presetId
      }

  let private setLikedTracksHandling (showNotification: ShowNotification) setLikedTracksHandling (shorPreset: Preset.Show) =
    fun presetId ->
      task {
        do! setLikedTracksHandling presetId

        do! showNotification Messages.Updated

        return! shorPreset presetId
      }

  let includeLikedTracks showNotification sendPresetInfo (includeLikedTracks: PresetSettings.IncludeLikedTracks) : PresetSettings.IncludeLikedTracks =
    setLikedTracksHandling showNotification includeLikedTracks sendPresetInfo

  let excludeLikedTracks showNotification sendPresetInfo (excludeLikedTracks: PresetSettings.ExcludeLikedTracks) : PresetSettings.ExcludeLikedTracks =
    setLikedTracksHandling showNotification excludeLikedTracks sendPresetInfo

  let ignoreLikedTracks showNotification sendPresetInfo (ignoreLikedTracks: PresetSettings.IgnoreLikedTracks) : PresetSettings.IgnoreLikedTracks =
    setLikedTracksHandling showNotification ignoreLikedTracks sendPresetInfo