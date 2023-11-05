[<RequireQualifiedAccess>]
module Generator.Bot.Telegram

open System.Text.RegularExpressions
open Domain.Core
open Domain.Workflows
open Infrastructure.Core
open Resources
open Shared.Services
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums
open Telegram.Bot.Types.ReplyMarkups
open Telegram.Core
open Telegram.Workflows
open Domain.Extensions
open Generator.Bot.Helpers

let escapeMarkdownString (str: string) = Regex.Replace(str, "([\(\)`\.#\-!])", "\$1")

let sendMessage (bot: ITelegramBotClient) userId : SendMessage =
  fun text ->
    bot.SendTextMessageAsync(
      (userId |> UserId.value |> ChatId),
      text |> escapeMarkdownString,
      parseMode = ParseMode.MarkdownV2
    )
    |> Task.map ignore

let sendButtons (bot: ITelegramBotClient) userId : SendButtons =
  fun text buttons ->
    let replyMarkup =
      buttons
      |> Seq.map(Seq.map(InlineKeyboardButton.WithCallbackData))
      |> InlineKeyboardMarkup

    bot.SendTextMessageAsync(
      (userId |> UserId.value |> ChatId),
      text |> escapeMarkdownString,
      parseMode = ParseMode.MarkdownV2,
      replyMarkup = replyMarkup
    )
    |> Task.map ignore

let replyToMessage (bot: ITelegramBotClient) userId (messageId: int) : ReplyToMessage =
  fun text ->
    bot.SendTextMessageAsync(
      (userId |> UserId.value |> ChatId),
      text |> escapeMarkdownString,
      parseMode = ParseMode.MarkdownV2,
      replyToMessageId = messageId
    )
    |> Task.map ignore

let sendKeyboard (bot: ITelegramBotClient) userId : SendKeyboard =
  fun text buttons ->
    let replyMarkup =
      buttons
      |> Seq.map Seq.toArray
      |> Seq.toArray
      |> ReplyKeyboardMarkup.op_Implicit

    bot.SendTextMessageAsync(
      (userId |> UserId.value |> ChatId),
      text |> escapeMarkdownString,
      parseMode = ParseMode.MarkdownV2,
      replyMarkup = replyMarkup
    )
    |> Task.map ignore

let editMessage (bot: ITelegramBotClient) messageId userId: EditMessage =
  fun text buttons ->
    let replyMarkup =
      buttons
      |> Seq.map(Seq.map(InlineKeyboardButton.WithCallbackData))
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
      replyMarkup = (InlineKeyboardButton(linkText, Url = link) |> Seq.singleton |> Seq.singleton |> InlineKeyboardMarkup)
    )
    |> Task.map ignore

let checkAuth (spotifyClientProvider: SpotifyClientProvider) : CheckAuth =
  UserId.value
  >> spotifyClientProvider.GetAsync
  >> Task.map (function
    | null -> Unauthorized
    | _ -> Authorized)

let private savePlaylistSize loadUser setPlaylistSize =
  fun userId playlistSize ->
    task{
      let! currentPresetId = loadUser userId |> Task.map (fun u -> u.CurrentPresetId |> Option.get)

      do! setPlaylistSize currentPresetId playlistSize
    }

let setPlaylistSize sendMessage sendSettingsMessage loadUser setPlaylistSize =
  fun userId size ->
    let savePlaylistSize = savePlaylistSize loadUser setPlaylistSize userId

    let onSuccess () =
      sendSettingsMessage userId

    let onError =
      function
      | PresetSettings.PlaylistSize.TooSmall -> sendMessage Messages.PlaylistSizeTooSmall
      | PresetSettings.PlaylistSize.TooBig -> sendMessage Messages.PlaylistSizeTooBig

    PresetSettings.PlaylistSize.tryCreate size
    |> Result.taskMap(savePlaylistSize)
    |> TaskResult.taskEither onSuccess onError

let includePlaylist replyToMessage loadUser (includePlaylist: Playlist.IncludePlaylist) : Playlist.Include =
  fun userId rawPlaylistId ->
    task{
      let! currentPresetId = loadUser userId |> Task.map (fun u -> u.CurrentPresetId |> Option.get)
      let includePlaylistResult = rawPlaylistId |> includePlaylist currentPresetId

      let onSuccess (playlist: IncludedPlaylist) =
        replyToMessage $"*{playlist.Name}* successfully included into current preset!"

      let onError =
        function
        | Playlist.IncludePlaylistError.IdParsing _ ->
          replyToMessage (System.String.Format(Messages.PlaylistIdCannotBeParsed, (rawPlaylistId |> RawPlaylistId.value)))
        | Playlist.IncludePlaylistError.MissingFromSpotify(Playlist.MissingFromSpotifyError id) ->
          replyToMessage (System.String.Format(Messages.PlaylistNotFoundInSpotify, id))

      return! includePlaylistResult |> TaskResult.taskEither onSuccess onError
    }

let excludePlaylist replyToMessage loadUser (excludePlaylist: Playlist.ExcludePlaylist) : Playlist.Exclude =
  fun userId rawPlaylistId ->
    task{
      let! currentPresetId = loadUser userId |> Task.map (fun u -> u.CurrentPresetId |> Option.get)

      let excludePlaylistResult = rawPlaylistId |> excludePlaylist currentPresetId

      let onSuccess (playlist: ExcludedPlaylist) =
        replyToMessage $"*{playlist.Name}* successfully excluded from current preset!"

      let onError =
        function
        | Playlist.ExcludePlaylistError.IdParsing _ ->
          replyToMessage (System.String.Format(Messages.PlaylistIdCannotBeParsed, (rawPlaylistId |> RawPlaylistId.value)))
        | Playlist.ExcludePlaylistError.MissingFromSpotify(Playlist.MissingFromSpotifyError id) ->
          replyToMessage (System.String.Format(Messages.PlaylistNotFoundInSpotify, id))

      return! excludePlaylistResult |> TaskResult.taskEither onSuccess onError
    }

let targetPlaylist replyToMessage loadUser (targetPlaylist: Playlist.TargetPlaylist) : Playlist.Target =
  fun userId rawPlaylistId ->
    task {
      let! currentPresetId = loadUser userId |> Task.map (fun u -> u.CurrentPresetId |> Option.get)

      let targetPlaylistResult = rawPlaylistId |> targetPlaylist currentPresetId

      let onSuccess (playlist: TargetedPlaylist) =
        replyToMessage $"*{playlist.Name}* successfully targeted for current preset!"

      let onError =
        function
        | Playlist.TargetPlaylistError.IdParsing _ ->
          replyToMessage (System.String.Format(Messages.PlaylistIdCannotBeParsed, (rawPlaylistId |> RawPlaylistId.value)))
        | Playlist.TargetPlaylistError.MissingFromSpotify(Playlist.MissingFromSpotifyError id) ->
          replyToMessage (System.String.Format(Messages.PlaylistNotFoundInSpotify, id))
        | Playlist.TargetPlaylistError.AccessError _ ->
          replyToMessage Messages.PlaylistIsReadonly

      return! targetPlaylistResult |> TaskResult.taskEither onSuccess onError
    }