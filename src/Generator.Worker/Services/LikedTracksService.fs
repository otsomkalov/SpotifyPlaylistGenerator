namespace Generator.Worker.Services

open System.Threading.Tasks
open Generator.Worker.Domain
open Microsoft.Extensions.Logging
open SpotifyAPI.Web
open Shared.Services

type LikedTracksService(_spotifyClientProvider: SpotifyClientProvider, _idsService: TracksIdsService, _logger: ILogger<LikedTracksService>) =
  let rec downloadIdsAsync' (userId: int64) (offset: int) =
    task {
      let client =
        _spotifyClientProvider.Get(userId)

      let! tracks = client.Library.GetTracks(LibraryTracksRequest(Offset = offset, Limit = 50))

      let! nextTracksIds =
        if tracks.Next = null then
          [] |> Task.FromResult
        else
          downloadIdsAsync' userId (offset + 50)

      let currentTracksIds =
        tracks.Items
        |> List.ofSeq
        |> List.map (fun x -> x.Track.Id)
        |> List.map RawTrackId.create

      return List.append nextTracksIds currentTracksIds
    }

  member _.ListIdsAsync userId refreshCache =
    task {
      let! tracksIds = _idsService.ReadOrDownloadAsync (string userId) (downloadIdsAsync' userId 0) refreshCache

      _logger.LogInformation("User with Telegram id {TelegramId} has {LikedTracksIdsCount} liked tracks", userId, tracksIds.Length)

      return tracksIds
    }
