module Domain.Tests.ExcludedPlaylist

open System.Threading.Tasks
open Domain.Core
open Domain.Repos
open Domain.Workflows
open Moq
open Xunit
open FsUnit.Xunit

[<Fact>]
let ``enable should enable disabled playlist`` () =
  let mock = Mock<IPresetRepo>()

  mock
    .Setup(fun m -> m.LoadPreset(Mocks.presetId))
    .ReturnsAsync(
      { Mocks.preset with
          ExcludedPlaylists =
            [ { Mocks.excludedPlaylist with
                  Enabled = false } ] }
    )

  let expected =
    { Mocks.preset with
        ExcludedPlaylists =
          [ { Mocks.excludedPlaylist with
                Enabled = true } ] }

  mock.Setup(fun m -> m.SavePreset(expected)).ReturnsAsync(())

  let sut = ExcludedPlaylist.enable mock.Object

  task {
    do! sut Mocks.presetId Mocks.excludedPlaylist.Id

    mock.VerifyAll()
  }

[<Fact>]
let ``disable should disable enabled playlist`` () =
  let mock = Mock<IPresetRepo>()

  mock
    .Setup(fun m -> m.LoadPreset(Mocks.presetId))
    .ReturnsAsync(
      { Mocks.preset with
          ExcludedPlaylists =
            [ { Mocks.excludedPlaylist with
                  Enabled = true } ] }
    )

  let expected =
    { Mocks.preset with
        ExcludedPlaylists =
          [ { Mocks.excludedPlaylist with
                Enabled = false } ] }

  mock.Setup(fun m -> m.SavePreset(expected)).ReturnsAsync(())

  let sut = ExcludedPlaylist.disable mock.Object

  task {
    do! sut Mocks.presetId Mocks.excludedPlaylist.Id

    mock.VerifyAll()
  }

[<Fact>]
let ``remove should remove playlist from preset`` () =
  let mock = Mock<IPresetRepo>()

  mock.Setup(fun m -> m.LoadPreset(Mocks.presetId)).ReturnsAsync(Mocks.preset)

  let expected =
    { Mocks.preset with
        ExcludedPlaylists = [] }

  mock.Setup(fun m -> m.SavePreset(expected)).ReturnsAsync(())

  let sut = ExcludedPlaylist.remove mock.Object

  task {
    do! sut Mocks.presetId Mocks.excludedPlaylist.Id

    mock.VerifyAll()
  }