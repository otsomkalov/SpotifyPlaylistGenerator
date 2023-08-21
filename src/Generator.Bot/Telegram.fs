[<RequireQualifiedAccess>]
module Generator.Bot.Telegram

open System.Text.RegularExpressions
open System.Threading.Tasks
open Database
open Domain.Core
open Domain.Workflows
open Infrastructure.Core
open Infrastructure.Helpers
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

let sendPresetInfo (bot: ITelegramBotClient) (loadPreset: User.LoadPreset) : SendPresetInfo =
  fun messageId userId presetId ->
    task {
      let! preset = loadPreset presetId |> Async.StartAsTask

      let presetId = presetId |> PresetId.value

      let keyboardMarkup =
        seq {
          seq {
            InlineKeyboardButton("Included playlists", CallbackData = $"p|{presetId}|ip|0")
            InlineKeyboardButton("Excluded playlists", CallbackData = $"p|{presetId}|ep|0")
            InlineKeyboardButton("Target playlists", CallbackData = $"p|{presetId}|tp|0")
          }

          seq { InlineKeyboardButton("Set as current", CallbackData = $"p|{presetId}|c") }
        }
        |> InlineKeyboardMarkup

      do!
        bot.EditMessageTextAsync(
          (userId |> UserId.value |> ChatId),
          messageId,
          $"Your preset info: {preset.Name}",
          replyMarkup = keyboardMarkup
        )
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

let escapeMarkdownString (str: string) = Regex.Replace(str, "(.)", "\$1")

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
