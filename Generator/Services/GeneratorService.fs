namespace Generator.Services

open Generator
open Microsoft.Extensions.Hosting

type GeneratorService
    (
        _likedTracksService: LikedTracksService,
        _historyPlaylistsService: HistoryPlaylistsService,
        _targetPlaylistService: TargetPlaylistService,
        _hostApplicationLifetime: IHostApplicationLifetime,
        _spotifyClientProvider: SpotifyClientProvider,
        _playlistsService: PlaylistsService
    ) =
    member _.generatePlaylist() =
        task {
            let! likedTracksIds = _likedTracksService.listIdsAsync
            let! historyTracksIds = _historyPlaylistsService.listTracksIdsAsync
            let! playlistsTracksIds = _playlistsService.listPlaylistsTracksIds

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
