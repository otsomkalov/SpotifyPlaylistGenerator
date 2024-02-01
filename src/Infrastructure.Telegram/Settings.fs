module Generator.Settings

[<CLIMutable>]
type TelegramSettings =
  { Token: string
    BotUrl: string }

  static member SectionName = "Telegram"
