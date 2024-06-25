module Domain.Tests.Preset

open System.Threading.Tasks
open Domain.Core
open Domain.Workflows
open Xunit
open Xunit
open FsUnit.Xunit
open otsom.fs.Extensions

let io: Preset.GenerateIO =
  { ListIncludedTracks = fun _ -> [] |> Task.FromResult
    ListExcludedTracks = fun _ -> failwith "todo"
    ListLikedTracks = fun _ -> failwith "todo"
    LoadPreset =
      fun presetId ->
        presetId |> should equal Mocks.presetMockId

        Mocks.presetMock |> Task.FromResult
    AppendTracks = fun _ -> failwith "todo"
    ReplaceTracks = fun _ -> failwith "todo"
    GetRecommendations = fun _ -> failwith "todo"

  }

[<Fact>]
let ``generate should return error if no included tracks`` () =

  let io =
    { io with
        LoadPreset =
          fun presetId ->
            presetId |> should equal Mocks.presetMockId

            { Mocks.presetMock with
                Settings =
                  { Mocks.presetSettingsMock with
                      RecommendationsEnabled = false
                      LikedTracksHandling = PresetSettings.LikedTracksHandling.Ignore } }
            |> Task.FromResult }

  let sut = Preset.generate io

  task {
    let! result = sut Mocks.presetMockId

    result |> should equal (Preset.GenerateError.NoIncludedTracks |> Error)
  }
