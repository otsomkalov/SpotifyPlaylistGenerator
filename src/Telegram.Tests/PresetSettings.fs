module Telegram.Tests.PresetSettings

open System
open System.Threading.Tasks
open Domain.Core
open Domain.Tests
open Telegram.Bot.Types.ReplyMarkups
open FsUnit
open Xunit
open Telegram.Workflows

[<Fact>]
let ``disableUniqueArtists should update preset and send show updated`` () =
  let disableUniqueArtists =
    fun presetId ->
      presetId |> should equal Preset.mockId
      Task.FromResult()

  let answerCallbackQuery = fun text -> Task.FromResult()

  let showPresetInfo =
    fun presetId ->
      presetId |> should equal Preset.mockId
      Task.FromResult()

  let sut =
    PresetSettings.disableUniqueArtists disableUniqueArtists answerCallbackQuery showPresetInfo

  sut Preset.mockId
