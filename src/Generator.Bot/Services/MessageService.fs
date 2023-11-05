namespace Generator.Bot.Services

open Resources
open System
open System.Threading.Tasks
open Domain.Core
open Domain.Workflows
open Infrastructure.Workflows
open Generator.Bot
open Generator.Bot.Services
open Microsoft.Extensions.Options
open Microsoft.FSharp.Core
open MongoDB.Driver
open Shared.Services
open Shared.Settings
open StackExchange.Redis
open Telegram.Bot
open Telegram.Bot.Types
open Generator.Bot.Helpers
open Telegram.Bot.Types.Enums
open Domain.Extensions
open Telegram.Helpers

type AuthState =
  | Authorized
  | Unauthorized

type MessageService
  (
    _generateCommandHandler: GenerateCommandHandler,
    _spotifyClientProvider: SpotifyClientProvider,
    _bot: ITelegramBotClient,
    loadUser: User.Load,
    loadPreset: Preset.Load,
    updatePreset: Preset.Update,
    _database: IMongoDatabase,
    _connectionMultiplexer: IConnectionMultiplexer,
    _spotifyOptions: IOptions<SpotifySettings>
  ) =

  let sendUserPresets sendMessage (message: Message) =
    let sendUserPresets = Telegram.Workflows.sendUserPresets sendMessage loadUser
    sendUserPresets (message.From.Id |> UserId)

  member this.ProcessAsync(message: Message) =
    let userId = message.From.Id |> UserId

    let sendMessage = Telegram.sendMessage _bot userId
    let sendLink = Telegram.sendLink _bot userId
    let sendKeyboard = Telegram.sendKeyboard _bot userId
    let replyToMessage = Telegram.replyToMessage _bot userId message.MessageId
    let sendButtons = Telegram.sendButtons _bot userId
    let askForReply = Telegram.askForReply _bot userId message.MessageId
    let savePreset = Preset.save _database
    let updateUser = User.update _database
    let createPreset = Preset.create savePreset loadUser updateUser userId

    let sendCurrentPresetInfo = Telegram.Workflows.sendCurrentPresetInfo loadUser loadPreset sendKeyboard
    let sendSettingsMessage = Telegram.Workflows.sendSettingsMessage loadUser loadPreset sendKeyboard
    let sendPresetInfo =
      Telegram.Workflows.sendPresetInfo loadPreset sendButtons
    let createPreset = Telegram.Workflows.Message.createPreset createPreset sendPresetInfo

    let sendLoginMessage () =
      let initState = Auth.initState _connectionMultiplexer
      let getLoginLink = Auth.getLoginLink _spotifyOptions

      let getLoginLink = Domain.Workflows.Auth.getLoginLink initState getLoginLink

      getLoginLink userId
      |> Task.bind (sendLink Messages.LoginToSpotify Buttons.Login)

    task{
      let! spotifyClient = _spotifyClientProvider.GetAsync message.From.Id

      let authState =
        if spotifyClient = null then
          AuthState.Unauthorized
        else
          AuthState.Authorized

      let parsePlaylistId = Playlist.parseId
      let loadFromSpotify = Playlist.loadFromSpotify spotifyClient

      return!
        match message.Type with
        | MessageType.Text ->
          let includePlaylist = Playlist.includePlaylist parsePlaylistId loadFromSpotify loadPreset updatePreset
          let includePlaylist = Telegram.includePlaylist replyToMessage loadUser includePlaylist

          let excludePlaylist = Playlist.excludePlaylist parsePlaylistId loadFromSpotify loadPreset updatePreset
          let excludePlaylist = Telegram.excludePlaylist replyToMessage loadUser excludePlaylist

          let targetPlaylist = Playlist.targetPlaylist parsePlaylistId loadFromSpotify loadPreset updatePreset
          let targetPlaylist = Telegram.targetPlaylist replyToMessage loadUser targetPlaylist

          match isNull message.ReplyToMessage with
          | false ->
            match (message.ReplyToMessage.Text, authState) with
            | Equals Messages.SendIncludedPlaylist, Unauthorized
            | Equals Messages.SendExcludedPlaylist, Unauthorized
            | Equals Messages.SendTargetedPlaylist, Unauthorized -> sendLoginMessage()

            | Equals Messages.SendPlaylistSize, _ ->
              match message.Text with
              | Int size ->
                let setPlaylistSize = Preset.setPlaylistSize loadPreset updatePreset
                let setPlaylistSize = Telegram.setPlaylistSize sendMessage sendSettingsMessage loadUser setPlaylistSize

                setPlaylistSize userId size
              | _ ->
                replyToMessage Messages.WrongPlaylistSize
            | Equals Messages.SendIncludedPlaylist, Authorized ->
              includePlaylist userId (Playlist.RawPlaylistId message.Text)
            | Equals Messages.SendExcludedPlaylist, Authorized ->
              excludePlaylist userId (Playlist.RawPlaylistId message.Text)
            | Equals Messages.SendTargetedPlaylist, Authorized ->
              targetPlaylist userId (Playlist.RawPlaylistId message.Text)
            | Equals Messages.SendPresetName, _ -> createPreset message.Text

            | _ -> replyToMessage "Unknown command"
          | _ ->
            match (message.Text, authState) with
            | StartsWith "/include", Unauthorized | StartsWith "/exclude", Unauthorized | StartsWith "/target", Unauthorized
            | Equals Buttons.IncludePlaylist, Unauthorized | Equals Buttons.ExcludePlaylist, Unauthorized | Equals Buttons.TargetPlaylist, Unauthorized
            | Equals Buttons.GeneratePlaylist, Unauthorized | StartsWith "/generate", Unauthorized
             -> sendLoginMessage()

            | Equals "/start", Unauthorized ->
              let initState = Auth.initState _connectionMultiplexer
              let getLoginLink = Auth.getLoginLink _spotifyOptions

              let getLoginLink = Domain.Workflows.Auth.getLoginLink initState getLoginLink

              getLoginLink userId
              |> Task.bind (sendLink Messages.Welcome Buttons.Login)
            | Equals "/start", Authorized ->
              sendCurrentPresetInfo userId
            | CommandWithData "/start" state, _ ->
              let tryGetAuth = Auth.tryGetCompletedAuth _connectionMultiplexer
              let getToken = Auth.getToken _spotifyOptions
              let saveCompletedAuth = Auth.saveCompletedAuth _connectionMultiplexer
              let createUserIfNotExists = User.createIfNotExists _database
              let sendErrorMessage =
                function
                | Auth.CompleteError.StateNotFound ->
                  replyToMessage "State not found. Try to login via fresh link."
                | Auth.CompleteError.StateDoesntBelongToUser ->
                  replyToMessage "State provided does not belong to your login request. Try to login via fresh link."

              let completeAuth = Domain.Workflows.Auth.complete tryGetAuth getToken saveCompletedAuth createUserIfNotExists

              completeAuth userId (state |> Auth.State.parse)
              |> TaskResult.taskEither (fun () -> sendCurrentPresetInfo userId) sendErrorMessage
            | Equals "/help", _ ->
              sendMessage Messages.Help
            | Equals "/guide", _ -> sendMessage Messages.Guide
            | Equals "/privacy", _ -> sendMessage Messages.Privacy
            | Equals "/faq", _ -> sendMessage Messages.FAQ
            | StartsWith "/generate", Authorized -> _generateCommandHandler.HandleAsync replyToMessage message
            | CommandWithData "/include" rawPlaylistId, Authorized ->
              if String.IsNullOrEmpty rawPlaylistId then
                replyToMessage "You have entered empty playlist url"
              else
                includePlaylist userId (rawPlaylistId |> Playlist.RawPlaylistId)
            | CommandWithData "/exclude" rawPlaylistId, Authorized ->
              if String.IsNullOrEmpty rawPlaylistId then
                replyToMessage "You have entered empty playlist url"
              else
                excludePlaylist userId (rawPlaylistId |> Playlist.RawPlaylistId)
            | CommandWithData "/target" rawPlaylistId, Authorized ->
              if String.IsNullOrEmpty rawPlaylistId then
                replyToMessage "You have entered empty playlist url"
              else
                targetPlaylist userId (rawPlaylistId |> Playlist.RawPlaylistId)
            | Equals Buttons.SetPlaylistSize, _ -> askForReply Messages.SendPlaylistSize
            | Equals Buttons.CreatePreset, _ -> askForReply Messages.SendPresetName
            | Equals Buttons.GeneratePlaylist, Authorized -> _generateCommandHandler.HandleAsync replyToMessage message
            | Equals Buttons.MyPresets, _ -> sendUserPresets sendButtons message
            | Equals Buttons.Settings, _ -> sendSettingsMessage userId
            | Equals Buttons.IncludePlaylist, Authorized -> askForReply Messages.SendIncludedPlaylist
            | Equals Buttons.ExcludePlaylist, Authorized -> askForReply Messages.SendExcludedPlaylist
            | Equals Buttons.TargetPlaylist, Authorized -> askForReply Messages.SendTargetedPlaylist
            | Equals "Back", _ -> sendCurrentPresetInfo userId

            | _ -> replyToMessage "Unknown command"
        | _ -> Task.FromResult()
    }
