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

printfn "Initialization..."

let client = SpotifyClient(settings.Token)

let listLikedTracksIds =
    client
    |> LikedTracksService.listLikedTracksIdsFromSpotify
    |> PlaylistService.listTracksIds "Liked.json"

let listHistoryTracksIds =
    PlaylistService.listTracksIdsFromSpotifyPlaylist client settings.HistoryPlaylistId
    |> PlaylistService.listTracksIds "History.json"

let listPlaylistsTracksIds =
    PlaylistsService.listPlaylistsTracksIds client settings.PlaylistsIds

let saveTracksToTargetPlaylist =
    SpotifyService.saveTracksToTargetPlaylist client settings.TargetPlaylistId

let saveTracksToHistoryPlaylist =
    SpotifyService.saveTracksToHistoryPlaylist client settings.HistoryPlaylistId

let saveTracks =
    saveTracksToTargetPlaylist
    >>= saveTracksToHistoryPlaylist

let generatePlaylistResult =
    PlaylistGenerator.generatePlaylist
        listLikedTracksIds
        listHistoryTracksIds
        listPlaylistsTracksIds
        saveTracks
    |> Async.AwaitTask
    |> Async.RunSynchronously

match generatePlaylistResult with
| Ok _ -> printfn "Playlist successfully generated"
| Error error -> eprintfn $"{error}"