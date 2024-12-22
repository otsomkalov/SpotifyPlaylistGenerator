module Domain.Tests.IncludedPlaylist

open Domain.Core
open Domain.Repos
open Domain.Workflows
open Moq
open Xunit

[<Fact>]
let ``enable should enable disabled playlist`` () =
  let mock = Mock<IPresetRepo>()

  mock
    .Setup(fun m -> m.LoadPreset(Mocks.presetId))
    .ReturnsAsync(
      { Mocks.preset with
          IncludedPlaylists =
            [ { Mocks.includedPlaylist with
                  Enabled = false } ] }
    )

  let expected =
    { Mocks.preset with
        IncludedPlaylists =
          [ { Mocks.includedPlaylist with
                Enabled = true } ] }

  mock.Setup(fun m -> m.SavePreset(expected)).ReturnsAsync(())

  let sut = IncludedPlaylist.enable mock.Object

  task {
    do! sut Mocks.presetId Mocks.includedPlaylist.Id

    mock.VerifyAll()
  }

[<Fact>]
let ``disable should disable enabled playlist`` () =
  let mock = Mock<IPresetRepo>()

  mock
    .Setup(fun m -> m.LoadPreset(Mocks.presetId))
    .ReturnsAsync(
      { Mocks.preset with
          IncludedPlaylists =
            [ { Mocks.includedPlaylist with
                  Enabled = true } ] }
    )

  let expected =
    { Mocks.preset with
        IncludedPlaylists =
          [ { Mocks.includedPlaylist with
                Enabled = false } ] }

  mock.Setup(fun m -> m.SavePreset(expected)).ReturnsAsync(())

  let sut = IncludedPlaylist.disable mock.Object

  task {
    do! sut Mocks.presetId Mocks.includedPlaylist.Id

    mock.VerifyAll()
  }

[<Fact>]
let ``remove should remove playlist from preset`` () =
  let mock = Mock<IPresetRepo>()

  mock.Setup(fun m -> m.LoadPreset(Mocks.presetId)).ReturnsAsync(Mocks.preset)

  let expected =
    { Mocks.preset with
        IncludedPlaylists = [] }

  mock.Setup(fun m -> m.SavePreset(expected)).ReturnsAsync(())

  let sut = IncludedPlaylist.remove mock.Object

  task {
    do! sut Mocks.presetId Mocks.includedPlaylist.Id

    mock.VerifyAll()
  }