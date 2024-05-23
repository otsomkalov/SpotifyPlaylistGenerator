module Domain.Tests.Tracks

open Domain.Core
open Domain.Workflows
open Xunit
open FsUnit

[<Fact>]
let ``uniqueByArtists `` () =
  // Arrange

  let track1 =
    { Id = TrackId "1"
      Artists = Set.ofList [ { Id = ArtistId "1" }; { Id = ArtistId "2" } ] }

  let track3 =
    { Id = TrackId "3"
      Artists = Set.ofList [ { Id = ArtistId "3" }; { Id = ArtistId "4" } ] }

  let tracks: Track list =
    [ track1
      { Id = TrackId "2"
        Artists = Set.ofList [ { Id = ArtistId "2" }; { Id = ArtistId "3" } ] }
      track3 ]

  // Act

  let result = Tracks.uniqueByArtists tracks

  // Assert

  result |> should equivalent [ track1; track3 ]
