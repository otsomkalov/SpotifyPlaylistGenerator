namespace Generator.Bot.Services

open Resources
open Microsoft.Extensions.Localization
open Shared.Data
open Telegram.Bot
open Telegram.Bot.Types

type SettingsCommandHandler
  (
    _bot: ITelegramBotClient,
    _context: AppDbContext,
    _localizer: IStringLocalizer<Messages>,
    _getSettingsMessageCommandHandler: GetSettingsMessageCommandHandler
  ) =
  member this.HandleAsync(message: Message) =
    task {
      let! user = _context.Users.FindAsync message.From.Id

      let text, replyMarkup =
        _getSettingsMessageCommandHandler.HandleAsync(user)

      _bot.SendTextMessageAsync(ChatId(message.Chat.Id), text, replyMarkup = replyMarkup)
      |> ignore

      return ()
    }
