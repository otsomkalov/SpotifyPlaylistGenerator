module Telegram.Tests.User

open System.Threading.Tasks
open Domain.Core
open Domain.Tests
open FsUnit.Xunit
open Xunit
open Telegram.Workflows
open otsom.fs.Bot

[<Fact>]
let ``sendCurrentPreset should show current preset details with actions keyboard if current preset is set`` () =
  let loadUser =
    fun userId ->
      userId |> should equal User.mock.Id

      { User.mock with
          CurrentPresetId = Some Mocks.presetId }
      |> Task.FromResult

  let getPreset =
    fun presetId ->
      presetId |> should equal Mocks.presetId
      Mocks.preset |> Task.FromResult

  let chatCtx =
    { new ISendKeyboard with
        member this.SendKeyboard =
          fun text keyboard ->
            keyboard |> Seq.length |> should equal 5
            Task.FromResult(Mocks.botMessageId) }

  let sut = User.sendCurrentPreset loadUser getPreset chatCtx

  sut User.mock.Id

[<Fact>]
let ``sendCurrentPreset should send "create preset" button if current preset is not set`` () =
  let loadUser =
    fun userId ->
      userId |> should equal User.mock.Id
      User.mock |> Task.FromResult

  let getPreset = fun _ -> failwith "todo"

  let chatCtx =
    { new ISendKeyboard with
        member this.SendKeyboard =
          fun text keyboard ->
            keyboard |> Seq.length |> should equal 2
            Task.FromResult(Mocks.botMessageId) }

  let sut = User.sendCurrentPreset loadUser getPreset chatCtx

  sut User.mock.Id