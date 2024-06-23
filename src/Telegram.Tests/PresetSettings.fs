module Telegram.Tests.PresetSettings

open System.Threading.Tasks
open Domain.Tests
open FsUnit
open Xunit
open Telegram.Workflows

[<Fact>]
let ``enableUniqueArtists should update preset and send show updated`` () =
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
    PresetSettings.enableUniqueArtists disableUniqueArtists answerCallbackQuery showPresetInfo

  sut Preset.mockId

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
