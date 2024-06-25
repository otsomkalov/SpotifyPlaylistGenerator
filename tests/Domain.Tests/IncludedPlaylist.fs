module Domain.Tests.IncludedPlaylist

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
          IncludedPlaylists =
            [ { Mocks.includedPlaylistMock with
                  Enabled = false } ] }
      |> Task.FromResult

  let updatePreset =
    fun preset ->

      preset
      |> should
        equal
        { Mocks.presetMock with
            IncludedPlaylists =
              [ { Mocks.includedPlaylistMock with
                    Enabled = true } ] }

      Task.FromResult()

  let sut = IncludedPlaylist.enable loadPreset updatePreset

  sut Mocks.presetMockId Mocks.includedPlaylistMock.Id


[<Fact>]
let ``disable should disable enabled playlist`` () =
  let loadPreset =
    fun presetId ->
      presetId |> should equal Mocks.presetMockId

      { Mocks.presetMock with
          IncludedPlaylists =
            [ { Mocks.includedPlaylistMock with
                  Enabled = true } ] }
      |> Task.FromResult

  let updatePreset =
    fun preset ->

      preset
      |> should
        equal
        { Mocks.presetMock with
            IncludedPlaylists =
              [ { Mocks.includedPlaylistMock with
                    Enabled = false } ] }

      Task.FromResult()

  let sut = IncludedPlaylist.disable loadPreset updatePreset

  sut Mocks.presetMockId Mocks.includedPlaylistMock.Id

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
            IncludedPlaylists = [] }

      Task.FromResult()

  let sut = IncludedPlaylist.remove loadPreset updatePreset

  sut Mocks.presetMockId Mocks.includedPlaylistMock.Id
