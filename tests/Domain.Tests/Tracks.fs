module Domain.Tests.Tracks

open Domain.Core
open Domain.Workflows
open Xunit
open FsUnit.Xunit
open Domain.Tests.Extensions

[<Fact>]
let ``uniqueByArtists returns tracks which have only unique artists`` () =
  // Arrange

  let tracks: Track list =
    [ Mocks.includedTrack
      Mocks.excludedTrack
      Mocks.likedTrack ]

  // Act

  let result = Tracks.uniqueByArtists tracks

  // Assert

  result |> should equalSeq [ Mocks.includedTrack; Mocks.likedTrack ]
