module Telegram.Tests.PresetSettings

open System.Threading.Tasks
open Domain.Tests
open FsUnit.Xunit
open Telegram.Bot.Types.ReplyMarkups
open Xunit
open Telegram.Workflows

let getPreset =
  fun presetId ->
    presetId |> should equal User.userPresetMock.Id

    Mocks.preset |> Task.FromResult

let editMessageButtons =
  fun text (replyMarkup: InlineKeyboardMarkup) ->
    replyMarkup.InlineKeyboard |> Seq.length |> should equal 6

    Task.FromResult()

[<Fact>]
let ``enableUniqueArtists should update preset and show updated`` () =
  let disableUniqueArtists =
    fun presetId ->
      presetId |> should equal Mocks.presetId

      Task.FromResult()

  let showNotification = fun _ -> Task.FromResult()

  let sut =
    PresetSettings.enableUniqueArtists getPreset editMessageButtons disableUniqueArtists showNotification

  sut Mocks.presetId

[<Fact>]
let ``disableUniqueArtists should update preset and show updated`` () =
  let disableUniqueArtists =
    fun presetId ->
      presetId |> should equal Mocks.presetId

      Task.FromResult()

  let showNotification = fun _ -> Task.FromResult()

  let showPresetInfo =
    fun presetId ->
      presetId |> should equal Mocks.presetId

      Task.FromResult()

  let sut =
    PresetSettings.disableUniqueArtists getPreset editMessageButtons disableUniqueArtists showNotification

  sut Mocks.presetId
