namespace Generator.Bot.Services

open Database
open Resources
open Microsoft.Extensions.Localization
open Telegram.Bot
open Telegram.Bot.Types
open Telegram.Bot.Types.Enums

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

      let! _ = _bot.SendTextMessageAsync(ChatId(message.Chat.Id), text, ParseMode.Markdown, replyMarkup = replyMarkup)

      return ()
    }
