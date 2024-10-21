module Domain.Tests.Preset

open System.Threading.Tasks
open Domain.Core
open Domain.Repos
open Domain.Workflows
open Moq
open Xunit
open FsUnit.Xunit
open Domain.Tests.Extensions

module Run =
  type IRunEnv =
    inherit IListPlaylistTracks
    inherit IListLikedTracks

  let getEnv () =
    let envMock = Mock<IRunEnv>()

    envMock
      .Setup(fun m -> m.ListPlaylistTracks(It.IsAny()))
      .ReturnsAsync([ Mocks.includedTrack ])

    envMock.Setup(fun m -> m.ListLikedTracks()).ReturnsAsync([ Mocks.likedTrack ])

    envMock

  let io: Preset.RunIO =
    { ListExcludedTracks =
        fun playlists ->
          playlists |> should equivalent [ Mocks.excludedPlaylist ]
          [ Mocks.excludedTrack ] |> Task.FromResult

      LoadPreset =
        fun presetId ->
          presetId |> should equal Mocks.presetId

          Mocks.preset |> Task.FromResult
      AppendTracks = fun _ _ -> Task.FromResult()
      ReplaceTracks = fun _ _ -> Task.FromResult()
      GetRecommendations =
        fun tracks ->
          tracks |> should equivalent [ Mocks.includedTrack.Id ]
          Task.FromResult [ Mocks.recommendedTrack ]
      Shuffler = id }

  [<Fact>]
  let ``loads only liked tracks from included playlists`` () =
    let includedPlaylist =
      { Mocks.includedPlaylist with
          LikedOnly = true }

    let preset =
      { Mocks.preset with
          IncludedPlaylists = [ includedPlaylist ] }

    let io =
      { io with
          LoadPreset = fun _ -> preset |> Task.FromResult
          ReplaceTracks =
            fun id tracks ->
              id |> should equal Mocks.targetedPlaylist.Id
              tracks |> should equal [ Mocks.includedTrack ]
              Task.FromResult() }

    let env = getEnv ()

    env
      .Setup(fun m -> m.ListPlaylistTracks(Mocks.includedPlaylistId))
      .ReturnsAsync([ Mocks.includedTrack; Mocks.excludedTrack ])

    env.Setup(fun m -> m.ListLikedTracks()).ReturnsAsync([ Mocks.includedTrack ])

    let sut = Preset.run env.Object io

    task {
      let! result = sut Mocks.presetId

      result |> should equal (Result<Preset, Preset.RunError>.Ok(preset))

      env.Verify(fun m -> m.ListPlaylistTracks(Mocks.includedPlaylistId))
      env.Verify(fun m -> m.ListLikedTracks())
    }

  [<Fact>]
  let ``loads all tracks from included playlists`` () =
    let io =
      { io with
          ReplaceTracks =
            fun id tracks ->
              id |> should equal Mocks.targetedPlaylist.Id
              tracks |> should equal [ Mocks.includedTrack; Mocks.recommendedTrack ]
              Task.FromResult() }

    let env = getEnv ()

    env
      .Setup(fun m -> m.ListPlaylistTracks(Mocks.includedPlaylistId))
      .ReturnsAsync([ Mocks.includedTrack; Mocks.recommendedTrack ])

    env.Setup(fun m -> m.ListLikedTracks()).ReturnsAsync([ Mocks.includedTrack ])

    let sut = Preset.run env.Object io

    task {
      let! result = sut Mocks.presetId

      result |> should equal (Result<Preset, Preset.RunError>.Ok(Mocks.preset))

      env.Verify(fun m -> m.ListPlaylistTracks(Mocks.includedPlaylistId))
      env.Verify((fun m -> m.ListLikedTracks()), Times.Never())
    }

  [<Fact>]
  let ``returns error if no potential tracks`` () =
    let io =
      { io with
          ListExcludedTracks =
            fun playlists ->
              playlists |> should equivalent [ Mocks.excludedPlaylist ]
              [ Mocks.includedTrack ] |> Task.FromResult }

    let env = getEnv ()

    let sut = Preset.run env.Object io

    task {
      let! result = sut Mocks.presetId

      result
      |> should equal (Result<Preset, _>.Error(Preset.RunError.NoPotentialTracks))
    }

  [<Fact>]
  let ``saves included tracks with liked`` () =
    let preset =
      { Mocks.preset with
          Settings =
            { Mocks.preset.Settings with
                LikedTracksHandling = PresetSettings.LikedTracksHandling.Include } }

    let io =
      { io with
          LoadPreset = fun _ -> preset |> Task.FromResult
          AppendTracks =
            fun _ tracks ->
              tracks |> should equalSeq [ Mocks.includedTrack; Mocks.likedTrack ]
              Task.FromResult()
          ReplaceTracks =
            fun _ tracks ->
              tracks |> should equalSeq [ Mocks.includedTrack; Mocks.likedTrack ]
              Task.FromResult() }

    let env = getEnv ()

    let sut = Preset.run env.Object io

    task {
      let! result = sut Mocks.presetId

      result |> should equal (Result<_, Preset.RunError>.Ok(preset))

      env.VerifyAll()
    }

  [<Fact>]
  let ``saves included tracks without liked`` () =
    let io =
      { io with
          AppendTracks =
            fun _ tracks ->
              tracks |> should equivalent [ Mocks.includedTrack ]
              Task.FromResult()
          ReplaceTracks =
            fun _ tracks ->
              tracks |> should equivalent [ Mocks.includedTrack ]
              Task.FromResult() }

    let env = getEnv ()

    let sut = Preset.run env.Object io

    task {
      let! result = sut Mocks.presetId

      result |> should equal (Result<_, Preset.RunError>.Ok(Mocks.preset))

      env.Verify(fun m -> m.ListPlaylistTracks(Mocks.includedPlaylistId))
    }

  [<Fact>]
  let ``saves included tracks excluding the excluded`` () =
    let io =
      { io with
          AppendTracks =
            fun _ tracks ->
              tracks |> should equivalent [ Mocks.includedTrack ]
              Task.FromResult()
          ReplaceTracks =
            fun _ tracks ->
              tracks |> should equivalent [ Mocks.includedTrack ]
              Task.FromResult() }

    let env = getEnv ()

    let sut = Preset.run env.Object io

    task {
      let! result = sut Mocks.presetId

      result |> should equal (Result<_, Preset.RunError>.Ok(Mocks.preset))

      env.Verify(fun m -> m.ListPlaylistTracks(Mocks.includedPlaylistId))
    }

  [<Fact>]
  let ``saves included tracks with recommendations`` () =
    let preset =
      { Mocks.preset with
          Settings =
            { Mocks.preset.Settings with
                RecommendationsEnabled = true } }

    let io =
      { io with
          LoadPreset = fun _ -> preset |> Task.FromResult
          AppendTracks =
            fun _ tracks ->
              tracks |> should equalSeq [ Mocks.recommendedTrack; Mocks.includedTrack ]
              Task.FromResult()
          ReplaceTracks =
            fun _ tracks ->
              tracks |> should equalSeq [ Mocks.recommendedTrack; Mocks.includedTrack ]
              Task.FromResult() }

    let env = getEnv ()

    let sut = Preset.run env.Object io

    task {
      let! result = sut Mocks.presetId

      result |> should equal (Result<_, Preset.RunError>.Ok(preset))

      env.Verify(fun m -> m.ListPlaylistTracks(Mocks.includedPlaylistId))
    }

  [<Fact>]
  let ``saves liked tracks with recommendations`` () =
    let preset =
      { Mocks.preset with
          Settings =
            { Mocks.preset.Settings with
                RecommendationsEnabled = true
                LikedTracksHandling = PresetSettings.LikedTracksHandling.Include } }

    let io =
      { io with
          LoadPreset = fun _ -> preset |> Task.FromResult
          GetRecommendations =
            fun tracks ->
              tracks |> should equivalent [ Mocks.likedTrack.Id ]
              Task.FromResult [ Mocks.recommendedTrack ]
          AppendTracks =
            fun _ tracks ->
              tracks |> should equalSeq [ Mocks.recommendedTrack; Mocks.likedTrack ]
              Task.FromResult()
          ReplaceTracks =
            fun _ tracks ->
              tracks |> should equalSeq [ Mocks.recommendedTrack; Mocks.likedTrack ]
              Task.FromResult() }

    let env = getEnv ()

    env
      .Setup(fun m -> m.ListPlaylistTracks(It.Is(fun id -> id = (Mocks.includedPlaylist.Id |> ReadablePlaylistId.value))))
      .ReturnsAsync([])

    let sut = Preset.run env.Object io

    task {
      let! result = sut Mocks.presetId

      result |> should equal (Result<_, Preset.RunError>.Ok(preset))

      env.Verify(fun m -> m.ListPlaylistTracks(It.Is(fun id -> id = (Mocks.includedPlaylist.Id |> ReadablePlaylistId.value))))
      env.Verify(fun m -> m.ListLikedTracks())
    }

  [<Fact>]
  let ``saves recommendations without excluded track`` () =
    let preset =
      { Mocks.preset with
          Settings =
            { Mocks.preset.Settings with
                RecommendationsEnabled = true } }

    let io =
      { io with
          LoadPreset = fun _ -> preset |> Task.FromResult
          GetRecommendations =
            fun tracks ->
              tracks |> should equivalent [ Mocks.includedTrack.Id ]
              Task.FromResult [ Mocks.excludedTrack ]
          AppendTracks =
            fun _ tracks ->
              tracks |> should equalSeq [ Mocks.includedTrack ]
              Task.FromResult()
          ReplaceTracks =
            fun _ tracks ->
              tracks |> should equalSeq [ Mocks.includedTrack ]
              Task.FromResult() }

    let env = getEnv ()

    let sut = Preset.run env.Object io

    task {
      let! result = sut Mocks.presetId

      result |> should equal (Result<_, Preset.RunError>.Ok(preset))

      env.Verify(fun m -> m.ListPlaylistTracks(Mocks.includedPlaylistId))
    }
