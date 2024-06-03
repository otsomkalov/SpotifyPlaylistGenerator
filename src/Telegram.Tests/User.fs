module Telegram.Tests.User

open System
open System.Threading.Tasks
open Domain.Core
open Domain.Tests
open Telegram.Bot.Types.ReplyMarkups
open FsUnit
open Xunit
open Telegram.Workflows
open Xunit.Sdk

[<Fact>]
let ``showCurrentPreset should send current preset and keyboard if current preset is set`` () =
  let loadUser =
    fun userId ->
      userId |> should equal User.mock.Id

      { User.mock with
          CurrentPresetId = Some Preset.mockId }
      |> Task.FromResult

  let getPreset =
    fun presetId ->
      presetId |> should equal Preset.mockId
      Preset.mock |> Task.FromResult

  let sendKeyboard =
    fun text (keyboard: ReplyKeyboardMarkup) ->
      keyboard.Keyboard |> Seq.length |> should equal 5
      Task.FromResult()

  let sut = User.showCurrentPreset loadUser getPreset sendKeyboard

  sut User.mock.Id

[<Fact>]
let ``showCurrentPreset should send create preset button if current preset is not set`` () =
  let loadUser =
    fun userId ->
      userId |> should equal User.mock.Id
      User.mock |> Task.FromResult

  let getPreset = fun presetId -> NotImplementedException() |> raise

  let sendKeyboard =
    fun text (keyboard: ReplyKeyboardMarkup) ->
      keyboard.Keyboard |> Seq.length |> should equal 2
      Task.FromResult()

  let sut = User.showCurrentPreset loadUser getPreset sendKeyboard

  sut User.mock.Id
