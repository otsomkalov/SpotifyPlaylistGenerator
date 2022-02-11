open Microsoft.Extensions.Configuration
open SpotifyAPI.Web
open Result

printfn "Initialization..."

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
    client
    |> LikedTracksService.listLikedTracksIdsFromSpotify

let listHistoryTracksIds =
    PlaylistService.listTracksIdsFromSpotifyPlaylist client settings.HistoryPlaylistId
    |> PlaylistService.listTracksIds "History.json"

let listPlaylistsTracksIds =
    PlaylistsService.listPlaylistsTracksIds client settings.PlaylistsIds

let saveTracksToTargetPlaylist =
    SpotifyService.saveTracksToTargetPlaylist client settings.TargetPlaylistId

let saveTracksToHistoryPlaylist =
    SpotifyService.saveTracksToHistoryPlaylist client settings.HistoryPlaylistId

let importTracksToSpotify =
    saveTracksToTargetPlaylist
    >>= saveTracksToHistoryPlaylist

let updateHistoryTracksIdsFile = FileService.saveIdsToFile "History.json"

let generatePlaylistResult =
    PlaylistGenerator.generatePlaylist
        listLikedTracksIds
        listHistoryTracksIds
        listPlaylistsTracksIds
        importTracksToSpotify
        updateHistoryTracksIdsFile
    |> Async.AwaitTask
    |> Async.RunSynchronously

match generatePlaylistResult with
| Ok _ -> printfn "Playlist successfully generated"
| Error error -> eprintfn $"{error}"