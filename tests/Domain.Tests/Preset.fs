module Domain.Tests.Preset

open System.Threading.Tasks
open Domain.Core
open Domain.Workflows
open Xunit
open FsUnit.Xunit
open Domain.Tests.Extensions

module Run =
  let io: Preset.RunIO =
    { ListPlaylistTracks =
        fun playlistId ->
          playlistId
          |> should equal (Mocks.includedPlaylist.Id |> ReadablePlaylistId.value)

          [ Mocks.includedTrack ] |> Task.FromResult

      ListExcludedTracks =
        fun playlists ->
          playlists |> should equivalent [ Mocks.excludedPlaylist ]
          [ Mocks.excludedTrack ] |> Task.FromResult

      ListLikedTracks = fun _ -> [ Mocks.likedTrack ] |> Task.FromResult
      LoadPreset =
        fun presetId ->
          presetId |> should equal Mocks.presetId

          Mocks.preset |> Task.FromResult
      AppendTracks = fun _ -> failwith "todo"
      ReplaceTracks = fun _ -> failwith "todo"
      GetRecommendations =
        fun tracks ->
          tracks |> should equivalent [ Mocks.includedTrack.Id ]
          Task.FromResult [ Mocks.recommendedTrack ]
      Shuffler = id }

  [<Fact>]
  let ``returns NoIncludedTracks if included playlist tracks are not liked`` () =
    let includedPlaylist =
      { Mocks.includedPlaylist with
          LikedOnly = true }

    let preset =
      { Mocks.preset with
          IncludedPlaylists = [ includedPlaylist ] }

    let io =
      { io with
          LoadPreset =
            fun presetId ->
              presetId |> should equal preset.Id
              preset |> Task.FromResult }

    let sut = Preset.run io

    task {
      let! result = sut Mocks.presetId

      result
      |> should equal (Result<Preset, _>.Error(Preset.RunError.NoIncludedTracks))
    }

  [<Fact>]
  let ``returns error if no potential tracks`` () =
    let io =
      { io with
          ListExcludedTracks =
            fun playlists ->
              playlists |> should equivalent [ Mocks.excludedPlaylist ]
              [ Mocks.includedTrack ] |> Task.FromResult }

    let sut = Preset.run io

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

    let sut = Preset.run io

    task {
      let! result = sut Mocks.presetId

      result |> should equal (Result<_, Preset.RunError>.Ok(preset))
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

    let sut = Preset.run io

    task {
      let! result = sut Mocks.presetId

      result |> should equal (Result<_, Preset.RunError>.Ok(Mocks.preset))
    }

  [<Fact>]
  let ``saves included tracks excluding the excluded`` () =
    let io =
      { io with
          ListPlaylistTracks =
            fun playlistId ->
              playlistId
              |> should equal (Mocks.includedPlaylist.Id |> ReadablePlaylistId.value)

              [ Mocks.includedTrack; Mocks.excludedTrack ] |> Task.FromResult
          AppendTracks =
            fun _ tracks ->
              tracks |> should equivalent [ Mocks.includedTrack ]
              Task.FromResult()
          ReplaceTracks =
            fun _ tracks ->
              tracks |> should equivalent [ Mocks.includedTrack ]
              Task.FromResult() }

    let sut = Preset.run io

    task {
      let! result = sut Mocks.presetId

      result |> should equal (Result<_, Preset.RunError>.Ok(Mocks.preset))
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

    let sut = Preset.run io

    task {
      let! result = sut Mocks.presetId

      result |> should equal (Result<_, Preset.RunError>.Ok(preset))
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
          ListPlaylistTracks =
            fun playlistId ->
              playlistId
              |> should equal (Mocks.includedPlaylist.Id |> ReadablePlaylistId.value)

              [] |> Task.FromResult
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

    let sut = Preset.run io

    task {
      let! result = sut Mocks.presetId

      result |> should equal (Result<_, Preset.RunError>.Ok(preset))
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

    let sut = Preset.run io

    task {
      let! result = sut Mocks.presetId

      result |> should equal (Result<_, Preset.RunError>.Ok(preset))
    }
