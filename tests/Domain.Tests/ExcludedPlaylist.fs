module Domain.Tests.ExcludedPlaylist

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
          ExcludedPlaylists =
            [ { Mocks.excludedPlaylistMock with
                  Enabled = false } ] }
      |> Task.FromResult

  let updatePreset =
    fun preset ->

      preset
      |> should
        equal
        { Mocks.presetMock with
            ExcludedPlaylists =
              [ { Mocks.excludedPlaylistMock with
                    Enabled = true } ] }

      Task.FromResult()

  let sut = ExcludedPlaylist.enable loadPreset updatePreset

  sut Mocks.presetMockId Mocks.excludedPlaylistMock.Id


[<Fact>]
let ``disable should disable enabled playlist`` () =
  let loadPreset =
    fun presetId ->
      presetId |> should equal Mocks.presetMockId

      { Mocks.presetMock with
          ExcludedPlaylists =
            [ { Mocks.excludedPlaylistMock with
                  Enabled = true } ] }
      |> Task.FromResult

  let updatePreset =
    fun preset ->

      preset
      |> should
        equal
        { Mocks.presetMock with
            ExcludedPlaylists =
              [ { Mocks.excludedPlaylistMock with
                    Enabled = false } ] }

      Task.FromResult()

  let sut = ExcludedPlaylist.disable loadPreset updatePreset

  sut Mocks.presetMockId Mocks.excludedPlaylistMock.Id

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
            ExcludedPlaylists =  [] }

      Task.FromResult()

  let sut = ExcludedPlaylist.remove loadPreset updatePreset

  sut Mocks.presetMockId Mocks.excludedPlaylistMock.Id

