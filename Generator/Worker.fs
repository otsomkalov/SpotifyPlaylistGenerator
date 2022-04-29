namespace Generator

open System.Threading
open Generator
open Generator.Services
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type Worker
    (
        _logger: ILogger<Worker>,
        _likedTracksService: LikedTracksService,
        _historyPlaylistsService: HistoryPlaylistsService,
        _targetPlaylistService: TargetPlaylistService,
        _hostApplicationLifetime: IHostApplicationLifetime
    ) =
    inherit BackgroundService()

    let runServiceAsync =
        task {
            let! likedTracksIds = _likedTracksService.listIdsAsync
            let! historyTracksIds = _historyPlaylistsService.listTracksIdsAsync
            let! playlistsTracksIds = _targetPlaylistService.listPlaylistsTracksIds

            let tracksIdsToImport =
                PlaylistGenerator.generatePlaylist likedTracksIds historyTracksIds playlistsTracksIds

            do! _targetPlaylistService.saveTracksAsync tracksIdsToImport
            do! _historyPlaylistsService.updateAsync tracksIdsToImport

            let newHistoryTracksIds =
                tracksIdsToImport
                |> List.map SpotifyTrackId.rawValue
                |> List.append historyTracksIds

            do! _historyPlaylistsService.updateCachedAsync newHistoryTracksIds
        }

    override _.ExecuteAsync(_: CancellationToken) =
        task {
            try
                do! runServiceAsync
            with
            | e -> _logger.LogError(e, "Error during generator execution:")

            _hostApplicationLifetime.StopApplication()
        }
