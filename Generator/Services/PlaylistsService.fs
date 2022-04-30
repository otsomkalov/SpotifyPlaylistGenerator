namespace Generator.Services

open Generator.Settings
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options

type PlaylistsService(_playlistService: PlaylistService, _options: IOptions<Settings>, _logger: ILogger<PlaylistsService>) =
    let _settings = _options.Value

    member _.listPlaylistsTracksIds =
        _logger.LogInformation("Listing playlists tracks ids")

        _playlistService.listTracksIdsAsync _settings.PlaylistsIds
