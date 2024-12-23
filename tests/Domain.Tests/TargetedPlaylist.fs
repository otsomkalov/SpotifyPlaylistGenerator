module Domain.Tests.TargetedPlaylist

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
          TargetedPlaylists =
            [ { Mocks.targetedPlaylist with
                  Enabled = false } ] }
    )

  let expected =
    { Mocks.preset with
        TargetedPlaylists =
          [ { Mocks.targetedPlaylist with
                Enabled = true } ] }

  mock.Setup(fun m -> m.SavePreset(expected)).ReturnsAsync(())

  let sut = TargetedPlaylist.enable mock.Object

  task {
    do! sut Mocks.presetId Mocks.targetedPlaylist.Id

    mock.VerifyAll()
  }

[<Fact>]
let ``disable should disable enabled playlist`` () =
  let mock = Mock<IPresetRepo>()

  mock
    .Setup(fun m -> m.LoadPreset(Mocks.presetId))
    .ReturnsAsync(
      { Mocks.preset with
          TargetedPlaylists =
            [ { Mocks.targetedPlaylist with
                  Enabled = true } ] }
    )

  let expected =
    { Mocks.preset with
        TargetedPlaylists =
          [ { Mocks.targetedPlaylist with
                Enabled = false } ] }

  mock.Setup(fun m -> m.SavePreset(expected)).ReturnsAsync(())


  let sut = TargetedPlaylist.disable mock.Object

  task {
    do! sut Mocks.presetId Mocks.targetedPlaylist.Id

    mock.VerifyAll()
  }

[<Fact>]
let ``appendTracks should disable playlist overwriting`` () =
  let mock = Mock<IPresetRepo>()

  mock
    .Setup(fun m -> m.LoadPreset(Mocks.presetId))
    .ReturnsAsync(
      { Mocks.preset with
          TargetedPlaylists =
            [ { Mocks.targetedPlaylist with
                  Overwrite = true } ] }
    )

  let expected =
    { Mocks.preset with
        TargetedPlaylists =
          [ { Mocks.targetedPlaylist with
                Overwrite = false } ] }

  mock.Setup(fun m -> m.SavePreset(expected)).ReturnsAsync(())


  let sut = TargetedPlaylist.appendTracks mock.Object

  task {
    do! sut Mocks.presetId Mocks.targetedPlaylist.Id

    mock.VerifyAll()
  }

[<Fact>]
let ``overwriteTracks should enable playlist overwriting`` () =
  let mock = Mock<IPresetRepo>()

  mock
    .Setup(fun m -> m.LoadPreset(Mocks.presetId))
    .ReturnsAsync(
      { Mocks.preset with
          TargetedPlaylists =
            [ { Mocks.targetedPlaylist with
                  Overwrite = false } ] }
    )

  let expected =
    { Mocks.preset with
        TargetedPlaylists =
          [ { Mocks.targetedPlaylist with
                Overwrite = true } ] }

  mock.Setup(fun m -> m.SavePreset(expected)).ReturnsAsync(())


  let sut = TargetedPlaylist.overwriteTracks mock.Object

  task {
    do! sut Mocks.presetId Mocks.targetedPlaylist.Id

    mock.VerifyAll()
  }

[<Fact>]
let ``remove should remove playlist from preset`` () =
  let mock = Mock<IPresetRepo>()

  mock.Setup(fun m -> m.LoadPreset(Mocks.presetId)).ReturnsAsync(Mocks.preset)

  let expected =
    { Mocks.preset with
        TargetedPlaylists = [] }

  mock.Setup(fun m -> m.SavePreset(expected)).ReturnsAsync(())


  let sut = TargetedPlaylist.remove mock.Object

  task {
    do! sut Mocks.presetId Mocks.targetedPlaylist.Id

    mock.VerifyAll()
  }