[<RequireQualifiedAccess>]
module Generator.Bot.Telegram

open System.Text.RegularExpressions
open System.Threading.Tasks
open Database
open Domain.Core
open Domain.Workflows
open Generator.Bot.Constants
open Infrastructure.Core
open Infrastructure.Helpers
open Infrastructure.Mapping
open Infrastructure.Workflows
open Resources
open Shared.Services
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums
open Telegram.Bot.Types.ReplyMarkups
open Microsoft.EntityFrameworkCore

[<Literal>]
let keyboardColumns = 4

[<Literal>]
let buttonsPerPage = 20

type SendUserPresets = UserId -> Task<unit>
type SendPresetInfo = int -> UserId -> PresetId -> Task<unit>
type SetCurrentPreset = string -> UserId -> PresetId -> Task<unit>
type EditMessage = int -> UserId -> string -> InlineKeyboardMarkup -> Task<unit>
type ShowIncludedPlaylists = int -> int -> UserId -> Preset -> Task<unit>
type ShowExcludedPlaylists = int -> int -> UserId -> Preset -> Task<unit>
type ShowTargetPlaylists = int -> int -> UserId -> Preset -> Task<unit>
type SetLikedTracksHandling = UserId -> PresetId -> PresetSettings.LikedTracksHandling -> Task<unit>
type SendCurrentPresetInfo = UserId -> Task<unit>
type GetPresetMessage = PresetId -> Task<string * string * string>
type SendSettingsMessage = UserId -> Task<unit>
type AskForPlaylistSize = UserId -> Task<unit>

type AuthState =
  | Authorized
  | Unauthorized

type CheckAuth = UserId -> Task<AuthState>

let sendUserPresets (bot: ITelegramBotClient) (listPresets: User.ListPresets) : SendUserPresets =
  fun userId ->
    task {
      let! presets = listPresets userId |> Async.StartAsTask

      let keyboardMarkup =
        presets
        |> Seq.map (fun p -> InlineKeyboardButton(p.Name, CallbackData = $"p|{p.Id |> PresetId.value}|i"))
        |> InlineKeyboardMarkup

      do!
        bot.SendTextMessageAsync((userId |> UserId.value |> ChatId), "Your presets", replyMarkup = keyboardMarkup)
        |> Task.map ignore
    }

let escapeMarkdownString (str: string) = Regex.Replace(str, "([`#\-])", "\$1")

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

let sendPresetInfo (bot: ITelegramBotClient) (getPresetMessage: GetPresetMessage) : SendPresetInfo =
  fun messageId userId presetId ->
    task {
      let! text, buttonText, buttonData = getPresetMessage presetId

      let presetId = presetId |> PresetId.value

      let keyboardMarkup =
        seq {
          seq {
            InlineKeyboardButton("Included playlists", CallbackData = $"p|%i{presetId}|ip|0")
            InlineKeyboardButton("Excluded playlists", CallbackData = $"p|%i{presetId}|ep|0")
            InlineKeyboardButton("Target playlists", CallbackData = $"p|%i{presetId}|tp|0")
          }

          seq { InlineKeyboardButton(buttonText, CallbackData = buttonData) }

          seq { InlineKeyboardButton("Set as current", CallbackData = $"p|%i{presetId}|c") }
        }
        |> InlineKeyboardMarkup

      do!
        bot.EditMessageTextAsync((userId |> UserId.value |> ChatId), messageId, text, ParseMode.MarkdownV2, replyMarkup = keyboardMarkup)
        |> Task.map ignore
    }

let setCurrentPreset (bot: ITelegramBotClient) (context: AppDbContext) : SetCurrentPreset =
  fun callbackQueryId userId presetId ->
    task {
      let userId = userId |> UserId.value
      let presetId = presetId |> PresetId.value

      let! user = context.Users.FirstOrDefaultAsync(fun u -> u.Id = userId)

      user.CurrentPresetId <- presetId

      user |> context.Update |> ignore

      do! context.SaveChangesAsync() |> Task.map ignore

      return! bot.AnswerCallbackQueryAsync(callbackQueryId, "Current playlist id successfully set!")
    }

let checkAuth (spotifyClientProvider: SpotifyClientProvider) : CheckAuth =
  UserId.value
  >> spotifyClientProvider.GetAsync
  >> Task.map (function
    | null -> Unauthorized
    | _ -> Authorized)

let editMessage (bot: ITelegramBotClient) : EditMessage =
  fun messageId userId text replyMarkup ->
    bot.EditMessageTextAsync(
      (userId |> UserId.value |> ChatId),
      messageId,
      text,
      ParseMode.MarkdownV2,
      replyMarkup = replyMarkup
    )
    |> Task.map ignore

let internal createPlaylistsPage page (playlists: 'a list) playlistToButton presetId =
  let remainingPlaylists = playlists[page * buttonsPerPage ..]
  let playlistsForPage = remainingPlaylists[.. buttonsPerPage - 1]

  let playlistsButtons =
    [ 0..keyboardColumns .. playlistsForPage.Length ]
    |> List.map (fun idx -> playlistsForPage |> List.skip idx |> List.takeSafe keyboardColumns)
    |> List.map (Seq.map playlistToButton)

  let presetId = presetId |> PresetId.value

  let backButton =
    InlineKeyboardButton("<< Back >>", CallbackData = $"p|{presetId}|i")

  let prevButton =
    if page > 0 then
      Some(InlineKeyboardButton("<< Prev", CallbackData = $"p|{presetId}|ip|{page - 1}"))
    else
      None

  let nextButton =
    if remainingPlaylists.Length > buttonsPerPage then
      Some(InlineKeyboardButton("Next >>", CallbackData = $"p|{presetId}|ip|{page + 1}"))
    else
      None

  let serviceButtons =
    match (prevButton, nextButton) with
    | Some pb, Some nb -> [ pb; backButton; nb ]
    | None, Some nb -> [ backButton; nb ]
    | Some pb, None -> [ pb; backButton ]
    | _ -> [ backButton ]

  Seq.append playlistsButtons (serviceButtons |> Seq.ofList |> Seq.singleton)
  |> InlineKeyboardMarkup

let showIncludedPlaylists editMessage : ShowIncludedPlaylists =
  let createButtonFromPlaylist =
    fun (playlist: IncludedPlaylist) ->
      InlineKeyboardButton(playlist.Name, CallbackData = $"ip|{playlist.Id |> ReadablePlaylistId.value |> PlaylistId.value}|i")

  fun messageId page userId preset ->
    let replyMarkup =
        createPlaylistsPage page preset.IncludedPlaylists createButtonFromPlaylist preset.Id

    editMessage messageId userId $"Preset *{preset.Name |> escapeMarkdownString}* has the next included playlists:" replyMarkup

let showExcludedPlaylists editMessage : ShowExcludedPlaylists =
  let createButtonFromPlaylist =
    fun (playlist: IncludedPlaylist) ->
      InlineKeyboardButton(playlist.Name, CallbackData = $"ep|{playlist.Id |> ReadablePlaylistId.value |> PlaylistId.value}|i")

  fun messageId page userId preset ->
    let replyMarkup =
        createPlaylistsPage page preset.ExcludedPlaylist createButtonFromPlaylist preset.Id

    editMessage messageId userId $"Preset *{preset.Name |> escapeMarkdownString}* has the next excluded playlists:" replyMarkup

let showTargetPlaylists editMessage : ShowTargetPlaylists =
  let createButtonFromPlaylist =
    fun (playlist: TargetPlaylist) ->
      InlineKeyboardButton(playlist.Name, CallbackData = $"tp|{playlist.Id |> WritablePlaylistId.value |> PlaylistId.value}|i")

  fun messageId page userId preset ->
    let replyMarkup =
      createPlaylistsPage page preset.TargetPlaylists createButtonFromPlaylist preset.Id

    editMessage messageId userId $"Preset *{preset.Name |> escapeMarkdownString}* has the next target playlists:" replyMarkup

let setLikedTracksHandling (bot: ITelegramBotClient) (setLikedTracksHandling: Preset.SetLikedTracksHandling) (sendPresetInfo : SendPresetInfo) callbackQueryId messageId : SetLikedTracksHandling =
  fun userId presetId likedTracksHandling ->
    task{
      do! setLikedTracksHandling presetId likedTracksHandling

      do! bot.AnswerCallbackQueryAsync(callbackQueryId, Messages.Updated)

      return! sendPresetInfo messageId userId presetId
    }

let askForPlaylistSize (bot: ITelegramBotClient) : AskForPlaylistSize =
  fun userId ->
    bot.SendTextMessageAsync(ChatId(userId |> UserId.value), Messages.SendPlaylistSize, replyMarkup = ForceReplyMarkup())
    |> Task.map ignore

let sendSettingsMessage (bot: ITelegramBotClient) (getCurrentPresetId: User.GetCurrentPresetId) (getPresetMessage: GetPresetMessage) : SendSettingsMessage =
  fun userId ->
    task {
      let! currentPresetId = getCurrentPresetId userId

      let! text, _, _ = getPresetMessage currentPresetId

      let replyMarkup =
        seq {
          seq { KeyboardButton(Messages.SetPlaylistSize) }
          seq { KeyboardButton("Back") }
        }
        |> ReplyKeyboardMarkup

      return!
        bot.SendTextMessageAsync(userId |> UserId.value |> ChatId, text, ParseMode.MarkdownV2, replyMarkup = replyMarkup)
        |> Task.map ignore
    }

let sendCurrentPresetInfo
  (bot: ITelegramBotClient)
  (getCurrentPresetId: User.GetCurrentPresetId)
  (getPresetMessage: GetPresetMessage)
  : SendCurrentPresetInfo =
  fun userId ->
    task {
      let! currentPresetId = getCurrentPresetId userId
      let! text, _, _ = getPresetMessage currentPresetId

      let replyMarkup =
        ReplyKeyboardMarkup(
          seq {
            seq { KeyboardButton(Messages.MyPresets) }
            seq { KeyboardButton(Messages.IncludePlaylist) }
            seq { KeyboardButton(Messages.Settings) }
          }
        )

      return!
        bot.SendTextMessageAsync(userId |> UserId.value |> ChatId, text, ParseMode.MarkdownV2, replyMarkup = replyMarkup)
        |> Task.map ignore
    }