namespace Generator

open System.Collections.Generic

type Settings() =
    member val Token = "" with get, set
    member val ClientId = "" with get, set
    member val ClientSecret = "" with get, set
    member val HistoryPlaylistsIds = List<string>() with get, set
    member val TargetPlaylistId = "" with get, set
    member val TargetHistoryPlaylistId = "" with get, set
    member val RefreshCache = false with get, set
    member val PlaylistsIds = List<string>() with get, set
