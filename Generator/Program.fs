open System.Threading.Tasks
open Microsoft.Extensions.Configuration
open Microsoft.FSharp.Control
open SpotifyAPI.Web
open Option

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

let updateHistoryTracksIdsFile = FileService.saveIdsToFile "History.json"

let historyTracksIds = listHistoryTracksIds |> Async.AwaitTask |> Async.RunSynchronously

let saveTracks =
    saveTracksToTargetPlaylist
    >== saveTracksToHistoryPlaylist
    >== updateHistoryTracksIdsFile historyTracksIds

let generatePlaylistResult =
    PlaylistGenerator.generatePlaylist
        listLikedTracksIds
        historyTracksIds
        listPlaylistsTracksIds
        saveTracks
    |> Async.AwaitTask
    |> Async.RunSynchronously

match generatePlaylistResult with
| Some() -> printfn "Playlist successfully generated"
| None -> eprintfn "There was an error during playlist generation"