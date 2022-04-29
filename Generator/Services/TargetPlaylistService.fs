namespace Generator.Services

open System.Collections.Generic
open Generator
open Microsoft.Extensions.Options
open SpotifyAPI.Web

type TargetPlaylistService(_playlistService: PlaylistService, _options: IOptions<Settings>, _client: ISpotifyClient) =
    let _settings = _options.Value

    member _.listPlaylistsTracksIds =
        _playlistService.listTracksIdsAsync _settings.PlaylistsIds

    member _.saveTracksAsync tracksIds =
        task {
            let replaceItemsRequest =
                tracksIds
                |> List.map SpotifyTrackId.value
                |> List<string>
                |> PlaylistReplaceItemsRequest

            printfn "Saving tracks to target playlist"

            let! _ = _client.Playlists.ReplaceItems(_settings.TargetPlaylistId, replaceItemsRequest)

            return ()
        }