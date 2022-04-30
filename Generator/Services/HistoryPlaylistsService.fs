namespace Generator.Services

open System.Collections.Generic
open Generator.Settings
open Generator
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open SpotifyAPI.Web

type HistoryPlaylistsService
    (
        _options: IOptions<Settings>,
        _playlistService: PlaylistService,
        _spotifyClientProvider: SpotifyClientProvider,
        _fileService: FileService,
        _logger: ILogger<HistoryPlaylistsService>
    ) =
    let _settings = _options.Value

    member _.listTracksIdsAsync =
        _logger.LogInformation("Listing history playlists tracks ids")

        _playlistService.listTracksIdsAsync _settings.HistoryPlaylistsIds

    member _.updateAsync tracksIds =
        task {
            _logger.LogInformation("Adding new tracks to history playlist")

            let addItemsRequest =
                tracksIds
                |> List.map SpotifyTrackId.value
                |> List<string>
                |> PlaylistAddItemsRequest

            printfn "Saving tracks to history playlist"

            let! _ = _spotifyClientProvider.Client.Playlists.AddItems(_settings.TargetHistoryPlaylistId, addItemsRequest)

            return ()
        }

    member _.updateCachedAsync =
        _logger.LogInformation("Updating history playlist cache file")

        _fileService.saveIdsAsync $"{_settings.TargetHistoryPlaylistId}.json"
