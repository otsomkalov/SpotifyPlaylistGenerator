module PlaylistGenerator

open System.Threading.Tasks
open Spotify

type ListLikedTracksIds = Task<RawTrackId seq>
type HistoryTracksIds = RawTrackId seq
type ListPlaylistsTracksIds = Task<RawTrackId seq>
type SaveTracksIdsToTargetPlaylist = SpotifyTrackId seq -> Task<SpotifyTrackId seq option>
type SaveTracksIdsToHistoryPlaylist = SpotifyTrackId seq -> Task<SpotifyTrackId seq option>

type UpdateHistoryTracksIdsFile = RawTrackId seq -> SpotifyTrackId seq -> Task<unit option>

type SaveTracks = SpotifyTrackId seq -> Task<unit option>

type GeneratePlaylist =
    ListLikedTracksIds
        -> HistoryTracksIds
        -> ListPlaylistsTracksIds
        -> SaveTracks
        -> Task<unit option>

let generatePlaylist: GeneratePlaylist =
    fun listLikedTracksIds historyTracksIds listPlaylistsTracksIds saveTracks ->
        task {
            let! likedTracksIds = listLikedTracksIds
            let! playlistsTracksIds = listPlaylistsTracksIds

            let tracksIdsToExclude =
                Seq.append likedTracksIds historyTracksIds
                |> Seq.distinct

            let tracksIdsToImport =
                playlistsTracksIds
                |> Seq.except tracksIdsToExclude
                |> Seq.shuffle
                |> Seq.take 20

            let spotifyTracksIdsToImport =
                tracksIdsToImport
                |> Seq.map SpotifyTrackId.create

            return! spotifyTracksIdsToImport |> saveTracks
        }
