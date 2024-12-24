module Domain.Tests.User

open System
open System.Threading.Tasks
open Domain.Repos
open Moq
open Xunit
open Domain.Core
open Domain.Workflows
open FsUnit.Xunit
open otsom.fs.Core
open otsom.fs.Extensions

[<Fact>]
let ``setCurrentPreset updates User.CurrentPresetId`` () =
  let repo = Mock<IUserRepo>()

  repo
    .Setup(fun m -> m.LoadUser(Mocks.userId))
    .ReturnsAsync(
      { Mocks.user with
          CurrentPresetId = None }
    )

  let expectedUser =
    { Mocks.user with
        CurrentPresetId = Some Mocks.presetId }

  repo.Setup(fun m -> m.SaveUser(expectedUser)).ReturnsAsync(())

  let sut = User.setCurrentPreset repo.Object

  task {
    do! sut Mocks.userId Mocks.presetId

    repo.VerifyAll()
  }

[<Fact>]
let ``removePreset removes preset`` () =
  let repo = Mock<IUserRepo>()

  repo
    .Setup(fun m -> m.LoadUser(Mocks.userId))
    .ReturnsAsync(
      { Mocks.user with
          CurrentPresetId = None }
    )

  let expectedUser =
    { Mocks.user with
        Presets = []
        CurrentPresetId = None }

  repo.Setup(fun m -> m.SaveUser(expectedUser)).ReturnsAsync(())

  let removePreset =
    fun presetId ->
      presetId |> should equal Mocks.presetId
      Task.FromResult()

  let sut = User.removePreset repo.Object removePreset

  task {
    do! sut Mocks.userId Mocks.presetId

    repo.VerifyAll()
  }

[<Fact>]
let ``createIfNotExists doesn't create attempt to create user if it already exists`` () =

  let exists =
    fun userId ->
      userId |> should equal Mocks.userId
      Task.FromResult true

  let create = fun _ -> raise (NotImplementedException())

  let sut = User.createIfNotExists exists create

  sut Mocks.userId

[<Fact>]
let ``createIfNotExists creates user if it does not exist`` () =

  let exists =
    fun userId ->
      userId |> should equal Mocks.userId
      Task.FromResult true

  let create =
    fun user ->
      user
      |> should
        equal
        { Id = Mocks.userId
          Presets = []
          CurrentPresetId = None }

      Task.FromResult()

  let sut = User.createIfNotExists exists create

  sut Mocks.userId