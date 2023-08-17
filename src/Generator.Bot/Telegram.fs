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

type SendUserPresets = UserId -> Task<unit>
type SendPresetInfo = int -> UserId -> PresetId -> Task<unit>
type SetCurrentPreset = string -> UserId -> PresetId -> Task<unit>
type ShowIncludedPlaylist = int -> UserId -> Preset -> Task<unit>
type ShowExcludedPlaylist = int -> UserId -> Preset -> Task<unit>

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
            InlineKeyboardButton("Included playlists", CallbackData = $"p|{presetId}|ip")
            InlineKeyboardButton("Excluded playlists", CallbackData = $"p|{presetId}|ep")
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

let showIncludedPlaylists (bot: ITelegramBotClient) : ShowIncludedPlaylist =
  let createButtonFromPlaylist =
    fun (playlist: IncludedPlaylist) ->
      InlineKeyboardButton(playlist.Name, CallbackData = $"ip|{playlist.Id |> ReadablePlaylistId.value |> PlaylistId.value}|i")

  fun messageId userId preset ->
    task {
      let playlistButtons =
        [ 0..keyboardColumns .. preset.IncludedPlaylists.Length ]
        |> List.map (fun idx -> preset.IncludedPlaylists |> List.skip idx |> List.takeSafe keyboardColumns)
        |> List.map (Seq.map createButtonFromPlaylist)

      let replyMarkup =
        Seq.append
          playlistButtons
          (InlineKeyboardButton("<< Back", CallbackData = $"p|{preset.Id |> PresetId.value}|i")
           |> Seq.singleton
           |> Seq.singleton)
        |> InlineKeyboardMarkup

      let! _ =
        bot.EditMessageTextAsync(
          (userId |> UserId.value |> ChatId),
          messageId,
          $"Preset *{preset.Name |> escapeMarkdownString}* has the next included playlists:",
          ParseMode.MarkdownV2,
          replyMarkup = replyMarkup
        )

      return ()
    }

let showExcludedPlaylists (bot: ITelegramBotClient) : ShowExcludedPlaylist =
  let createButtonFromPlaylist =
    fun (playlist: IncludedPlaylist) ->
      InlineKeyboardButton(playlist.Name, CallbackData = $"ep|{playlist.Id |> ReadablePlaylistId.value |> PlaylistId.value}|i")

  fun messageId userId preset ->
    task {
      let playlistButtons =
        [ 0..keyboardColumns .. preset.ExcludedPlaylist.Length ]
        |> List.map (fun idx -> preset.ExcludedPlaylist |> List.skip idx |> List.takeSafe keyboardColumns)
        |> List.map (Seq.map createButtonFromPlaylist)

      let replyMarkup =
        Seq.append
          playlistButtons
          (InlineKeyboardButton("<< Back", CallbackData = $"p|{preset.Id |> PresetId.value}|i")
           |> Seq.singleton
           |> Seq.singleton)
        |> InlineKeyboardMarkup

      let! _ =
        bot.EditMessageTextAsync(
          (userId |> UserId.value |> ChatId),
          messageId,
          $"Preset *{preset.Name |> escapeMarkdownString}* has the next excluded playlists:",
          ParseMode.MarkdownV2,
          replyMarkup = replyMarkup
        )

      return ()
    }
