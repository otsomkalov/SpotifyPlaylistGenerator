module PlaylistGenerator

open Spotify

type LikedTracksIds = RawTrackId seq
type HistoryTracksIds = RawTrackId seq
type PlaylistsTracksIds = RawTrackId seq

type GeneratePlaylist = LikedTracksIds -> HistoryTracksIds -> PlaylistsTracksIds -> SpotifyTrackId list

let generatePlaylist: GeneratePlaylist =
    fun likedTracksIds historyTracksIds playlistsTracksIds ->
        let tracksIdsToExclude =
            Seq.append likedTracksIds historyTracksIds

        playlistsTracksIds
        |> Seq.except tracksIdsToExclude
        |> Seq.shuffle
        |> Seq.take 20
        |> Seq.map SpotifyTrackId.create
        |> Seq.toList
