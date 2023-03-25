namespace Generator.Worker.Services

open System.Threading.Tasks
open Infrastructure
open Microsoft.Extensions.Logging
open SpotifyAPI.Web
open Shared.Services
open StackExchange.Redis

type LikedTracksService(_spotifyClientProvider: SpotifyClientProvider, _logger: ILogger<LikedTracksService>, _cache: IDatabase) =
  let rec downloadIdsAsync' (offset: int) (userId: int64) =
    task {
      let client = _spotifyClientProvider.Get(userId)

      let! tracks = client.Library.GetTracks(LibraryTracksRequest(Offset = offset, Limit = 50))

      let! nextTracksIds =
        if tracks.Next = null then
          [] |> Task.FromResult
        else
          downloadIdsAsync' (offset + 50) userId

      let currentTracksIds = tracks.Items |> List.ofSeq |> List.map (fun x -> x.Track.Id)

      return List.append nextTracksIds currentTracksIds
    }

  member _.ListIdsAsync userId refreshCache =
    task {
      let listOrRefresh =
        Cache.listOrRefresh _cache (downloadIdsAsync' 0) refreshCache

      let! tracksIds = listOrRefresh userId

      _logger.LogInformation("User with Telegram id {TelegramId} has {LikedTracksIdsCount} liked tracks", userId, tracksIds.Length)

      return tracksIds
    }
