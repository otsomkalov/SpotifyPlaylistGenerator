module internal Infrastructure.Spotify

open SpotifyAPI.Web

let getTracksIds (tracks: FullTrack seq) =
  tracks
  |> Seq.filter (fun t -> isNull t |> not)
  |> Seq.map (fun t -> t.Id)
  |> Seq.toList