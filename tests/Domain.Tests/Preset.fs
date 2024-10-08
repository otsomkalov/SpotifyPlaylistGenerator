﻿module Domain.Tests.Preset

open System.Threading.Tasks
open Domain.Core
open Domain.Workflows
open Xunit
open FsUnit.Xunit
open Domain.Tests.Extensions

module Run =
  let io: Preset.RunIO =
    { ListIncludedTracks =
        fun playlists ->
          playlists |> should equivalent [ Mocks.includedPlaylist ]
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
      |> should equal (Result<unit, Preset.RunError>.Error(Preset.RunError.NoPotentialTracks))
    }

  [<Fact>]
  let ``saves included tracks with liked`` () =
    let io =
      { io with
          LoadPreset =
            fun _ ->
              { Mocks.preset with
                  Settings =
                    { Mocks.preset.Settings with
                        LikedTracksHandling = PresetSettings.LikedTracksHandling.Include } }
              |> Task.FromResult
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

      result |> should equal (Result<unit, Preset.RunError>.Ok())
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

      result |> should equal (Result<unit, Preset.RunError>.Ok())
    }

  [<Fact>]
  let ``saves included tracks excluding the excluded`` () =
    let io =
      { io with
          ListIncludedTracks =
            fun playlists ->
              playlists |> should equivalent [ Mocks.includedPlaylist ]
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

      result |> should equal (Result<unit, Preset.RunError>.Ok())
    }

  [<Fact>]
  let ``saves included tracks with recommendations`` () =
    let io =
      { io with
          LoadPreset =
            fun _ ->
              { Mocks.preset with
                  Settings =
                    { Mocks.preset.Settings with
                        RecommendationsEnabled = true } }
              |> Task.FromResult
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

      result |> should equal (Result<unit, Preset.RunError>.Ok())
    }

  [<Fact>]
  let ``saves liked tracks with recommendations`` () =
    let io =
      { io with
          LoadPreset =
            fun _ ->
              { Mocks.preset with
                  Settings =
                    { Mocks.preset.Settings with
                        RecommendationsEnabled = true
                        LikedTracksHandling = PresetSettings.LikedTracksHandling.Include } }
              |> Task.FromResult
          ListIncludedTracks =
            fun playlists ->
              playlists |> should equivalent [ Mocks.includedPlaylist ]
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

      result |> should equal (Result<unit, Preset.RunError>.Ok())
    }

  [<Fact>]
  let ``saves recommendations without excluded track`` () =
    let io =
      { io with
          LoadPreset =
            fun _ ->
              { Mocks.preset with
                  Settings =
                    { Mocks.preset.Settings with
                        RecommendationsEnabled = true } }
              |> Task.FromResult
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

      result |> should equal (Result<unit, Preset.RunError>.Ok())
    }
