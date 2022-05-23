module Generator.Worker.Services.PlaylistService

open System.Threading.Tasks
open Shared

let private readOrDownloadTracksIdsAsync env userId refreshCache playlistId =
  TracksIdsService.readOrDownloadAsync env $"{playlistId}.json" (Spotify.listPlaylistTracksIds userId playlistId) refreshCache

let listTracksIdsAsync env userId playlistsIds refreshCache =
  task {
    let! playlistsTracks =
      playlistsIds
      |> Seq.map (readOrDownloadTracksIdsAsync env userId refreshCache)
      |> Task.WhenAll

    return playlistsTracks |> List.concat |> List.distinct
  }
