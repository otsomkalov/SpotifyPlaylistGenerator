module Telegram.Tests.PresetSettings

open System.Threading.Tasks
open Domain.Tests
open FsUnit.Xunit
open Xunit
open Telegram.Workflows

[<Fact>]
let ``enableUniqueArtists should update preset and show updated`` () =
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
    PresetSettings.enableUniqueArtists disableUniqueArtists showNotification showPresetInfo

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
    PresetSettings.disableUniqueArtists disableUniqueArtists showNotification showPresetInfo

  sut Mocks.presetId
