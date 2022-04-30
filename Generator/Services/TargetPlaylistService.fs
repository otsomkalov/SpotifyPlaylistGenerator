namespace Generator.Services

open System.Collections.Generic
open Generator.Settings
open Generator
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open SpotifyAPI.Web

type TargetPlaylistService
    (
        _playlistService: PlaylistService,
        _options: IOptions<Settings>,
        _spotifyClientProvider: SpotifyClientProvider,
        _logger: ILogger<TargetPlaylistService>
    ) =
    let _settings = _options.Value

    member _.saveTracksAsync tracksIds =
        task {
            _logger.LogInformation("Saving tracks ids to target playlist")

            let replaceItemsRequest =
                tracksIds
                |> List.map SpotifyTrackId.value
                |> List<string>
                |> PlaylistReplaceItemsRequest

            printfn "Saving tracks to target playlist"

            let! _ = _spotifyClientProvider.Client.Playlists.ReplaceItems(_settings.TargetPlaylistId, replaceItemsRequest)

            return ()
        }
