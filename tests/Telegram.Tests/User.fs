module Telegram.Tests.User

open System
open System.Threading.Tasks
open Domain.Core
open Domain.Tests
open Telegram.Bot.Types.ReplyMarkups
open FsUnit.Xunit
open Xunit
open Telegram.Workflows

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

  let sendKeyboard =
    fun text (keyboard: ReplyKeyboardMarkup) ->
      keyboard.Keyboard |> Seq.length |> should equal 5
      Task.FromResult()

  let sut = User.sendCurrentPreset loadUser getPreset sendKeyboard

  sut User.mock.Id

[<Fact>]
let ``sendCurrentPreset should send "create preset" button if current preset is not set`` () =
  let loadUser =
    fun userId ->
      userId |> should equal User.mock.Id
      User.mock |> Task.FromResult

  let getPreset = fun _ -> failwith "todo"

  let sendKeyboard =
    fun text (keyboard: ReplyKeyboardMarkup) ->
      keyboard.Keyboard |> Seq.length |> should equal 2
      Task.FromResult()

  let sut = User.sendCurrentPreset loadUser getPreset sendKeyboard

  sut User.mock.Id
