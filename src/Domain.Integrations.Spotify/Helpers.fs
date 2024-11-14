namespace Domain.Integrations.Spotify

open System
open Domain.Core
open SpotifyAPI.Web

module Helpers =
  let getTracksIds (tracks: FullTrack seq) : Track list =
    tracks
    |> Seq.filter (isNull >> not)
    |> Seq.filter (_.Id >> isNull >> not)
    |> Seq.map (fun st ->
      { Id = TrackId st.Id
        Artists = st.Artists |> Seq.map (fun a -> { Id = ArtistId a.Id }) |> Set.ofSeq })
    |> Seq.toList

  let (|ApiException|_|) (ex: exn) =
    match ex with
    | :? AggregateException as aggregateException ->
      aggregateException.InnerExceptions
      |> Seq.tryPick (fun e -> e :?> APIException |> Option.ofObj)
    | :? APIException as e -> Some e
    | _ -> None