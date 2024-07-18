module Domain.Tests.TargetedPlaylist

open System.Threading.Tasks
open Domain.Core
open Domain.Workflows
open Xunit
open FsUnit.Xunit

[<Fact>]
let ``enable should enable disabled playlist`` () =
  let loadPreset =
    fun presetId ->
      presetId |> should equal Mocks.presetId

      { Mocks.preset with
          TargetedPlaylists =
            [ { Mocks.targetedPlaylist with
                  Enabled = false } ] }
      |> Task.FromResult

  let updatePreset =
    fun preset ->

      preset
      |> should
        equal
        { Mocks.preset with
            TargetedPlaylists =
              [ { Mocks.targetedPlaylist with
                    Enabled = true } ] }

      Task.FromResult()

  let sut = TargetedPlaylist.enable loadPreset updatePreset

  sut Mocks.presetId Mocks.targetedPlaylist.Id


[<Fact>]
let ``disable should disable enabled playlist`` () =
  let loadPreset =
    fun presetId ->
      presetId |> should equal Mocks.presetId

      { Mocks.preset with
          TargetedPlaylists =
            [ { Mocks.targetedPlaylist with
                  Enabled = true } ] }
      |> Task.FromResult

  let updatePreset =
    fun preset ->

      preset
      |> should
        equal
        { Mocks.preset with
            TargetedPlaylists =
              [ { Mocks.targetedPlaylist with
                    Enabled = false } ] }

      Task.FromResult()

  let sut = TargetedPlaylist.disable loadPreset updatePreset

  sut Mocks.presetId Mocks.targetedPlaylist.Id

[<Fact>]
let ``appendTracks should disable playlist overwriting`` () =
  let loadPreset =
    fun presetId ->
      presetId |> should equal Mocks.presetId

      { Mocks.preset with
          TargetedPlaylists =
            [ { Mocks.targetedPlaylist with
                  Overwrite = true } ] }
      |> Task.FromResult

  let updatePreset =
    fun preset ->

      preset
      |> should
        equal
        { Mocks.preset with
            TargetedPlaylists =
              [ { Mocks.targetedPlaylist with
                    Overwrite = false } ] }

      Task.FromResult()

  let sut = TargetedPlaylist.appendTracks loadPreset updatePreset

  sut Mocks.presetId Mocks.targetedPlaylist.Id

[<Fact>]
let ``overwriteTracks should enable playlist overwriting`` () =
  let loadPreset =
    fun presetId ->
      presetId |> should equal Mocks.presetId

      { Mocks.preset with
          TargetedPlaylists =
            [ { Mocks.targetedPlaylist with
                  Overwrite = false } ] }
      |> Task.FromResult

  let updatePreset =
    fun preset ->

      preset
      |> should
        equal
        { Mocks.preset with
            TargetedPlaylists =
              [ { Mocks.targetedPlaylist with
                    Overwrite = true } ] }

      Task.FromResult()

  let sut = TargetedPlaylist.overwriteTracks loadPreset updatePreset

  sut Mocks.presetId Mocks.targetedPlaylist.Id

[<Fact>]
let ``remove should remove playlist from preset`` () =
  let loadPreset =
    fun presetId ->
      presetId |> should equal Mocks.presetId

      Mocks.preset |> Task.FromResult

  let updatePreset =
    fun preset ->

      preset
      |> should
        equal
        { Mocks.preset with
            TargetedPlaylists = [] }

      Task.FromResult()

  let sut = TargetedPlaylist.remove loadPreset updatePreset

  sut Mocks.presetId Mocks.targetedPlaylist.Id
