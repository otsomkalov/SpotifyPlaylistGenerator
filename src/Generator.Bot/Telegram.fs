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
open Helpers

[<Literal>]
let keyboardColumns = 4

[<Literal>]
let buttonsPerPage = 20

type SendUserPresets = UserId -> Task<unit>
type SendPresetInfo = PresetId -> Task<unit>
type SetCurrentPreset = UserId -> PresetId -> Task<unit>
type SendMessage = string -> InlineKeyboardMarkup -> Task<unit>
type EditMessage = string -> InlineKeyboardMarkup -> Task<unit>
type AnswerCallbackQuery = string -> Task
type Page = Page of int

type ShowIncludedPlaylists = PresetId -> Page -> Task<unit>
type ShowExcludedPlaylists = PresetId -> Page -> Task<unit>
type ShowTargetPlaylists = PresetId -> Page -> Task<unit>

type SetLikedTracksHandling = PresetId -> PresetSettings.LikedTracksHandling -> Task<unit>
type SendCurrentPresetInfo = UserId -> Task<unit>
type GetPresetMessage = PresetId -> Task<string * string * string>
type SendSettingsMessage = UserId -> Task<unit>
type AskForPlaylistSize = UserId -> Task<unit>

type ShowIncludedPlaylist = PresetId -> ReadablePlaylistId -> Task<unit>
type ShowExcludedPlaylist = PresetId -> ReadablePlaylistId -> Task<unit>
type ShowTargetPlaylist = PresetId -> WritablePlaylistId -> Task<unit>

type OverwriteTargetPlaylist = PresetId -> WritablePlaylistId -> Task<unit>
type AppendToTargetPlaylist = PresetId -> WritablePlaylistId -> Task<unit>

type RemoveIncludedPlaylist = PresetId -> ReadablePlaylistId -> Task<unit>
type RemoveExcludedPlaylist = PresetId -> ReadablePlaylistId -> Task<unit>
type RemoveTargetPlaylist = PresetId -> WritablePlaylistId -> Task<unit>

[<RequireQualifiedAccess>]
type Action =
  | ShowPresetInfo of presetId: PresetId
  | SetCurrentPreset of presetId: PresetId

  | ShowIncludedPlaylists of presetId: PresetId * page: Page
  | ShowExcludedPlaylists of presetId: PresetId * page: Page
  | ShowTargetPlaylists of presetId: PresetId * page: Page

  | ShowIncludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId
  | ShowExcludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId
  | ShowTargetPlaylist of presetId: PresetId * playlistId: WritablePlaylistId

  | RemoveIncludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId
  | RemoveExcludedPlaylist of presetId: PresetId * playlistId: ReadablePlaylistId
  | RemoveTargetPlaylist of presetId: PresetId * playlistId: WritablePlaylistId

  | AppendToTargetPlaylist of presetId: PresetId * playlistId: WritablePlaylistId
  | OverwriteTargetPlaylist of presetId: PresetId * playlistId: WritablePlaylistId

  | AskForPlaylistSize

  | IncludeLikedTracks of presetId: PresetId
  | ExcludeLikedTracks of presetId: PresetId
  | IgnoreLikedTracks of presetId: PresetId

let parseAction (str: string) =
  match str with
  | _ ->
    match str.Split("|") with
    | [| "p"; Int id; "i" |] -> PresetId id |> Action.ShowPresetInfo
    | [| "p"; Int id; "c" |] -> PresetId id |> Action.SetCurrentPreset

    | [| "p"; Int id; "ip"; Int page |] -> Action.ShowIncludedPlaylists(PresetId id, (Page page))
    | [| "p"; Int id; "ep"; Int page |] -> Action.ShowExcludedPlaylists(PresetId id, (Page page))
    | [| "p"; Int id; "tp"; Int page |] -> Action.ShowTargetPlaylists(PresetId id, (Page page))

    | [| "p"; Int presetId; "ip"; playlistId; "i" |] ->
      Action.ShowIncludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)
    | [| "p"; Int presetId; "ep"; playlistId; "i" |] ->
      Action.ShowExcludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)
    | [| "p"; Int presetId; "tp"; playlistId; "i" |] ->
      Action.ShowTargetPlaylist(PresetId presetId, PlaylistId playlistId |> WritablePlaylistId)

    | [| "p"; Int presetId; "ip"; playlistId; "rm" |] ->
      Action.RemoveIncludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)
    | [| "p"; Int presetId; "ep"; playlistId; "rm" |] ->
      Action.RemoveExcludedPlaylist(PresetId presetId, PlaylistId playlistId |> ReadablePlaylistId)
    | [| "p"; Int presetId; "tp"; playlistId; "rm" |] ->
      Action.RemoveTargetPlaylist(PresetId presetId, PlaylistId playlistId |> WritablePlaylistId)

    | [| "p"; Int presetId; "tp"; playlistId; "a" |] ->
      Action.AppendToTargetPlaylist(PresetId presetId, PlaylistId playlistId |> WritablePlaylistId)
    | [| "p"; Int presetId; "tp"; playlistId; "o" |] ->
      Action.OverwriteTargetPlaylist(PresetId presetId, PlaylistId playlistId |> WritablePlaylistId)

    | [| "p"; Int presetId; CallbackQueryConstants.includeLikedTracks |] -> Action.IncludeLikedTracks(PresetId presetId)
    | [| "p"; Int presetId; CallbackQueryConstants.excludeLikedTracks |] -> Action.ExcludeLikedTracks(PresetId presetId)
    | [| "p"; Int presetId; CallbackQueryConstants.ignoreLikedTracks |] -> Action.IgnoreLikedTracks(PresetId presetId)

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

let sendPresetInfo (bot: ITelegramBotClient) (getPresetMessage: GetPresetMessage) messageId userId : SendPresetInfo =
  fun presetId ->
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

let setCurrentPreset (bot: ITelegramBotClient) (context: AppDbContext) callbackQueryId : SetCurrentPreset =
  fun userId presetId ->
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

let sendMessage (bot: ITelegramBotClient) userId : SendMessage =
  fun text replyMarkup ->
    bot.SendTextMessageAsync(
      (userId |> UserId.value |> ChatId),
      text,
      ParseMode.MarkdownV2,
      replyMarkup = replyMarkup
    )
    |> Task.map ignore

let editMessage (bot: ITelegramBotClient) messageId userId: EditMessage =
  fun text replyMarkup ->
    bot.EditMessageTextAsync(
      (userId |> UserId.value |> ChatId),
      messageId,
      text,
      ParseMode.MarkdownV2,
      replyMarkup = replyMarkup
    )
    |> Task.map ignore

let answerCallbackQuery (bot: ITelegramBotClient) callbackQueryId : AnswerCallbackQuery =
  fun text ->
    bot.AnswerCallbackQueryAsync(callbackQueryId, text)

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

let showIncludedPlaylists (loadPreset: Preset.Load) (editMessage: EditMessage) : ShowIncludedPlaylists =
  let createButtonFromPlaylist presetId =
    fun (playlist: IncludedPlaylist) ->
      InlineKeyboardButton(
        playlist.Name,
        CallbackData = sprintf "p|%i|ip|%s|i" (presetId |> PresetId.value) (playlist.Id |> ReadablePlaylistId.value |> PlaylistId.value)
      )

  fun presetId page ->
    let createButtonFromPlaylist = createButtonFromPlaylist presetId

    task {
      let! preset = loadPreset presetId

      let replyMarkup =
        createPlaylistsPage page preset.IncludedPlaylists createButtonFromPlaylist preset.Id

      return! editMessage $"Preset *{preset.Name |> escapeMarkdownString}* has the next included playlists:" replyMarkup
    }

let showExcludedPlaylists (loadPreset: Preset.Load) (editMessage: EditMessage) : ShowExcludedPlaylists =
  let createButtonFromPlaylist presetId =
    fun (playlist: IncludedPlaylist) ->
      InlineKeyboardButton(
        playlist.Name,
        CallbackData = sprintf "p|%i|ep|%s|i" (presetId |> PresetId.value) (playlist.Id |> ReadablePlaylistId.value |> PlaylistId.value)
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
      InlineKeyboardButton(
        playlist.Name,
        CallbackData = sprintf "p|%i|tp|%s|i" (presetId |> PresetId.value) (playlist.Id |> WritablePlaylistId.value |> PlaylistId.value)
      )

  fun presetId page ->
    let createButtonFromPlaylist = createButtonFromPlaylist presetId

    task {
      let! preset = loadPreset presetId

      let replyMarkup =
        createPlaylistsPage page preset.TargetPlaylists createButtonFromPlaylist preset.Id

      return! editMessage $"Preset *{preset.Name |> escapeMarkdownString}* has the next target playlists:" replyMarkup
    }

let setLikedTracksHandling (bot: ITelegramBotClient) (setLikedTracksHandling: Preset.SetLikedTracksHandling) (sendPresetInfo : SendPresetInfo) callbackQueryId : SetLikedTracksHandling =
  fun presetId likedTracksHandling ->
    task{
      do! setLikedTracksHandling presetId likedTracksHandling

      do! bot.AnswerCallbackQueryAsync(callbackQueryId, Messages.Updated)

      return! sendPresetInfo presetId
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

      let replyMarkup =
        seq {
          seq {
            InlineKeyboardButton(
              "Remove",
              CallbackData =
                sprintf "p|%i|ip|%s|rm" (presetId |> PresetId.value) (playlistId |> ReadablePlaylistId.value |> PlaylistId.value)
            )
          }

          seq { InlineKeyboardButton("<< Back >>", CallbackData = sprintf "p|%i|ip|%i" (presetId |> PresetId.value) 0) }
        }
        |> InlineKeyboardMarkup

      return! editMessage messageText replyMarkup
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
            InlineKeyboardButton(
              "Remove",
              CallbackData =
                sprintf "p|%i|ep|%s|rm" (presetId |> PresetId.value) (playlistId |> ReadablePlaylistId.value |> PlaylistId.value)
            )
          }

          seq { InlineKeyboardButton("<< Back >>", CallbackData = sprintf "p|%i|ep|%i" (presetId |> PresetId.value) 0) }
        }
        |> InlineKeyboardMarkup

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

      let replyMarkup =
        seq {
          seq { InlineKeyboardButton(buttonText, CallbackData = buttonData) }
          seq { InlineKeyboardButton("Remove", CallbackData = sprintf "p|%i|tp|%s|rm" presetId' playlistId') }

          seq { InlineKeyboardButton("<< Back >>", CallbackData = sprintf "p|%i|tp|%i" presetId' 0) }
        }
        |> InlineKeyboardMarkup

      return! editMessage messageText replyMarkup
    }

let removeIncludedPlaylist (bot: ITelegramBotClient) callbackQueryId : RemoveIncludedPlaylist =
  fun presetId playlistId ->
    task {
      do! bot.AnswerCallbackQueryAsync(callbackQueryId, "Not implemented yet")

      return ()
    }

let removeExcludedPlaylist (bot: ITelegramBotClient) callbackQueryId : RemoveExcludedPlaylist =
  fun presetId playlistId ->
    task {
      do! bot.AnswerCallbackQueryAsync(callbackQueryId, "Not implemented yet")

      return ()
    }

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