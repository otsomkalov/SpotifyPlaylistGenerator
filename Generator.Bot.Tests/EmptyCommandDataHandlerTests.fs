module Generator.Bot.Tests.EmptyCommandDataHandlerTests

open System.Threading
open Generator.Bot.Services
open Shared.AppEnv
open Telegram.Bot
open Telegram.Bot.Requests
open Telegram.Bot.Requests.Abstractions
open Telegram.Bot.Types
open Xunit
open System.Threading.Tasks

[<Fact>]
let Test =
  task{
    let bot =
      { new ITelegramBotClient with
          member _.MakeRequestAsync(request: IRequest<'a>, cancellationToken: CancellationToken): Task<'a> =
            task {
              let a = 0
              return EditMessageTextRequest()
            } }



    let m = Message(Chat = Chat(Id = 1), MessageId = 1)

    let env = AppEnv(null, bot, null, null)

    EmptyCommandDataHandler.handle env m
  }
