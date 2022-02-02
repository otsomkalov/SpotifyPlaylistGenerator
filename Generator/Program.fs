open Microsoft.Extensions.Configuration
open SpotifyAPI.Web
open Result

[<CLIMutable>]
type Settings =
    { Token: string
      HistoryPlaylistId: string
      TargetPlaylistId: string
      PlaylistsIds: string seq }

let configuration =
    ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build()

let settings = configuration.Get<Settings>()

let client = SpotifyClient(settings.Token)

let listLikedTracksIds =
    LikedTracksService.listLikedTracksIds client

let listHistoryTracksIds =
    HistoryTracksService.listHistoryTracksIds client settings.HistoryPlaylistId

let listPlaylistsTracksIds =
    PlaylistsService.listPlaylistsTracksIds client settings.PlaylistsIds

let saveTracksToTargetPlaylist =
    SpotifyService.saveTracksToTargetPlaylist client settings.TargetPlaylistId

let saveTracksToHistoryPlaylist =
    SpotifyService.saveTracksToHistoryPlaylist client settings.HistoryPlaylistId

let saveTracks =
    saveTracksToTargetPlaylist
    >>= saveTracksToHistoryPlaylist

let enumerableResultAsyncFunc =
    PlaylistGenerator.generatePlaylist
        listLikedTracksIds
        listHistoryTracksIds
        listPlaylistsTracksIds
        saveTracks
    |> Async.AwaitTask
    |> Async.RunSynchronously
