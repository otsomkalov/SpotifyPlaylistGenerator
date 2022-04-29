open Microsoft.Extensions.Configuration
open Microsoft.FSharp.Control
open Spotify
open SpotifyAPI.Web

printfn "Initialization..."

[<CLIMutable>]
type Settings =
    { Token: string
      HistoryPlaylistsIds: string list
      TargetPlaylistId: string
      TargetHistoryPlaylistId: string
      RefreshCache: bool
      PlaylistsIds: string list }

let configuration =
    ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build()

let settings = configuration.Get<Settings>()

let client = SpotifyClient(settings.Token)

let executeAsync =
    task {
        let! likedTracksIds =
            FileService.loadIdsFromFile "LikedTracks.json" (LikedTracksService.listLikedTracksIdsFromSpotify client) settings.RefreshCache

        let! historyTracksIds = PlaylistsService.listPlaylistsTracksIds client settings.HistoryPlaylistsIds settings.RefreshCache

        let! playlistsTracksIds = PlaylistsService.listPlaylistsTracksIds client settings.PlaylistsIds settings.RefreshCache

        let saveTracksToTargetPlaylist =
            SpotifyService.saveTracksToTargetPlaylist client settings.TargetPlaylistId

        let saveTracksToHistoryPlaylist =
            SpotifyService.saveTracksToHistoryPlaylist client settings.TargetHistoryPlaylistId

        let updateHistoryTracksIdsFile =
            FileService.saveIdsToFile $"{settings.TargetHistoryPlaylistId}.json"

        let tracksIdsToImport =
            PlaylistGenerator.generatePlaylist likedTracksIds historyTracksIds playlistsTracksIds

        do! saveTracksToTargetPlaylist tracksIdsToImport
        do! saveTracksToHistoryPlaylist tracksIdsToImport

        let newHistoryTracksIds =
            tracksIdsToImport
            |> List.map SpotifyTrackId.rawValue
            |> List.append historyTracksIds

        do! updateHistoryTracksIdsFile newHistoryTracksIds
    }

let generatePlaylistResult =
    executeAsync
    |> Async.AwaitTask
    |> Async.RunSynchronously
