module PlaylistGenerator

open Generator

type LikedTracksIds = RawTrackId list
type HistoryTracksIds = RawTrackId list
type PlaylistsTracksIds = RawTrackId list

type GeneratePlaylist = LikedTracksIds -> HistoryTracksIds -> PlaylistsTracksIds -> SpotifyTrackId list

let generatePlaylist: GeneratePlaylist =
    fun likedTracksIds historyTracksIds playlistsTracksIds ->
        let tracksIdsToExclude =
            List.append likedTracksIds historyTracksIds

        playlistsTracksIds
        |> List.except tracksIdsToExclude
        |> List.shuffle
        |> List.take 20
        |> List.map SpotifyTrackId.create
