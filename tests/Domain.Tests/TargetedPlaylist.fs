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
      presetId |> should equal Mocks.presetMockId

      { Mocks.presetMock with
          TargetedPlaylists =
            [ { Mocks.targetedPlaylistMock with
                  Enabled = false } ] }
      |> Task.FromResult

  let updatePreset =
    fun preset ->

      preset
      |> should
        equal
        { Mocks.presetMock with
            TargetedPlaylists =
              [ { Mocks.targetedPlaylistMock with
                    Enabled = true } ] }

      Task.FromResult()

  let sut = TargetedPlaylist.enable loadPreset updatePreset

  sut Mocks.presetMockId Mocks.targetedPlaylistMock.Id


[<Fact>]
let ``disable should disable enabled playlist`` () =
  let loadPreset =
    fun presetId ->
      presetId |> should equal Mocks.presetMockId

      { Mocks.presetMock with
          TargetedPlaylists =
            [ { Mocks.targetedPlaylistMock with
                  Enabled = true } ] }
      |> Task.FromResult

  let updatePreset =
    fun preset ->

      preset
      |> should
        equal
        { Mocks.presetMock with
            TargetedPlaylists =
              [ { Mocks.targetedPlaylistMock with
                    Enabled = false } ] }

      Task.FromResult()

  let sut = TargetedPlaylist.disable loadPreset updatePreset

  sut Mocks.presetMockId Mocks.targetedPlaylistMock.Id

[<Fact>]
let ``appendTracks should disable playlist overwriting`` () =
  let loadPreset =
    fun presetId ->
      presetId |> should equal Mocks.presetMockId

      { Mocks.presetMock with
          TargetedPlaylists =
            [ { Mocks.targetedPlaylistMock with
                  Overwrite = true } ] }
      |> Task.FromResult

  let updatePreset =
    fun preset ->

      preset
      |> should
        equal
        { Mocks.presetMock with
            TargetedPlaylists =
              [ { Mocks.targetedPlaylistMock with
                    Overwrite = false } ] }

      Task.FromResult()

  let sut = TargetedPlaylist.appendTracks loadPreset updatePreset

  sut Mocks.presetMockId Mocks.targetedPlaylistMock.Id

[<Fact>]
let ``overwriteTracks should enable playlist overwriting`` () =
  let loadPreset =
    fun presetId ->
      presetId |> should equal Mocks.presetMockId

      { Mocks.presetMock with
          TargetedPlaylists =
            [ { Mocks.targetedPlaylistMock with
                  Overwrite = false } ] }
      |> Task.FromResult

  let updatePreset =
    fun preset ->

      preset
      |> should
        equal
        { Mocks.presetMock with
            TargetedPlaylists =
              [ { Mocks.targetedPlaylistMock with
                    Overwrite = true } ] }

      Task.FromResult()

  let sut = TargetedPlaylist.overwriteTracks loadPreset updatePreset

  sut Mocks.presetMockId Mocks.targetedPlaylistMock.Id

[<Fact>]
let ``remove should remove playlist from preset`` () =
  let loadPreset =
    fun presetId ->
      presetId |> should equal Mocks.presetMockId

      Mocks.presetMock |> Task.FromResult

  let updatePreset =
    fun preset ->

      preset
      |> should
        equal
        { Mocks.presetMock with
            TargetedPlaylists = [] }

      Task.FromResult()

  let sut = TargetedPlaylist.remove loadPreset updatePreset

  sut Mocks.presetMockId Mocks.targetedPlaylistMock.Id
