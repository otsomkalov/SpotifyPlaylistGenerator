namespace Generator.Services

open System.Collections.Generic
open Generator
open Microsoft.Extensions.Options
open SpotifyAPI.Web

type HistoryPlaylistsService
    (
        _options: IOptions<Settings>,
        _playlistService: PlaylistService,
        _client: ISpotifyClient,
        _fileService: FileService
    ) =
    let _settings = _options.Value

    member _.listTracksIdsAsync =
        _playlistService.listTracksIdsAsync _settings.HistoryPlaylistsIds

    member _.updateAsync tracksIds =
        task {
            let addItemsRequest =
                tracksIds
                |> List.map SpotifyTrackId.value
                |> List<string>
                |> PlaylistAddItemsRequest

            printfn "Saving tracks to history playlist"

            let! _ = _client.Playlists.AddItems(_settings.TargetHistoryPlaylistId, addItemsRequest)

            return ()
        }

    member _.updateCachedAsync =
        _fileService.saveIdsAsync $"{_settings.TargetHistoryPlaylistId}.json"
