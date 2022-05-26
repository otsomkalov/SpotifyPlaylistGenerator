module Generator.Bot.Services.StartCommandHandler

open Shared
open Telegram.Bot.Types
open Generator.Bot.Helpers
open Telegram.Bot.Types.ReplyMarkups
open Resources

let private sendMessageAsync env (message: Message) =
  task {
    let replyMarkup =
      ReplyKeyboardMarkup(seq { seq { KeyboardButton(Messages.Settings) } })

    return! Bot.replyToMessageWithMarkup env message.Chat.Id "You've successfully logged in!" message.MessageId replyMarkup
  }

let private setSpotifyClient env (message: Message) (data: string) =
  task {
    Spotify.getClientBySpotifyId env data
    |> Spotify.setClient env message.From.Id

    return! sendMessageAsync env message
  }

let handleCommandDataAsync' env (message: Message) (spotifyId: string) =
  task {
    let spotifyClient =
      Spotify.getClient env message.From.Id

    return!
      if spotifyClient = null then
        setSpotifyClient env message spotifyId
      else
        sendMessageAsync env message
  }

let handleCommandDataAsync env (message: Message) (spotifyId: string) =
  let spotifyClient =
    Spotify.getClientBySpotifyId env spotifyId

  if spotifyClient = null then

    UnauthorizedUserCommandHandler.handle env message
  else
    handleCommandDataAsync' env message spotifyId

let handleEmptyCommandAsync env (message: Message) =
  task {
    let! userExists = Db.userExists env message.From.Id

    if not userExists then
      do! Db.createUser env message.From.Id

    return! UnauthorizedUserCommandHandler.handle env message
  }

let handle env (message: Message) =
  match message.Text with
  | CommandData spotifyId -> handleCommandDataAsync env message spotifyId
  | _ -> handleEmptyCommandAsync env message
