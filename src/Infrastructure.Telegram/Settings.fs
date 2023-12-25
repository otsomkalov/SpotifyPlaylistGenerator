module Generator.Settings

module TelegramSettings =
  [<Literal>]
  let SectionName = "Telegram"

type TelegramSettings() =
  member val Token = "" with get, set
  member val BotUrl = "" with get, set