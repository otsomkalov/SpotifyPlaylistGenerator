namespace Generator.Worker.Services


open Database
open Domain.Core
open Microsoft.Extensions.Logging
open Shared.Services
open Shared.QueueMessages
open Generator.Worker.Extensions
open Telegram.Bot
open Telegram.Bot.Types
open Microsoft.EntityFrameworkCore
open Domain.Workflows
open Infrastructure.Helpers
open System.Threading.Tasks

type GeneratorService
  (
    _likedTracksService: LikedTracksService,
    _targetPlaylistService: TargetPlaylistService,
    _spotifyClientProvider: SpotifyClientProvider,
    _logger: ILogger<GeneratorService>,
    _bot: ITelegramBotClient,
    _context: AppDbContext
  ) =

  member this.GeneratePlaylistAsync(queueMessage: GeneratePlaylistMessage, listPlaylistTracks: Playlist.ListTracks) =
    task {
      _logger.LogInformation("Received request to generate playlist for user with Telegram id {TelegramId}", queueMessage.TelegramId)

      (ChatId(queueMessage.TelegramId), "Generating playlist...")
      |> _bot.SendTextMessageAsync
      |> ignore

      let! user =
        _context
          .Users
          .AsNoTracking()
          .Include(fun x -> x.SourcePlaylists)
          .Include(fun x -> x.HistoryPlaylists)
          .FirstOrDefaultAsync(fun u -> u.Id = queueMessage.TelegramId)

      let! likedTracksIds = _likedTracksService.ListIdsAsync queueMessage.TelegramId queueMessage.RefreshCache
      let! includedTracks =
        user.SourcePlaylists
        |> Seq.map (fun p -> listPlaylistTracks p.Url)
        |> Task.WhenAll
        |> Task.map List.concat

      let! excludedTracks =
        user.HistoryPlaylists
        |> Seq.map (fun p -> listPlaylistTracks p.Url)
        |> Task.WhenAll
        |> Task.map List.concat

      let excludedTracksIds, includedTracksIds =
        match user.Settings.IncludeLikedTracks |> Option.ofNullable with
        | Some v when v = true -> excludedTracks, includedTracks @ likedTracksIds
        | Some v when v = false -> likedTracksIds @ excludedTracks, includedTracks
        | None -> excludedTracks, includedTracks

      _logger.LogInformation(
        "User with Telegram id {TelegramId} has {TracksToExcludeCount} tracks to exclude",
        queueMessage.TelegramId,
        excludedTracksIds.Length
      )

      let potentialTracksIds =
        includedTracksIds
        |> List.except excludedTracksIds

      _logger.LogInformation(
        "User with Telegram id {TelegramId} has {PotentialTracksCount} potential tracks",
        queueMessage.TelegramId,
        potentialTracksIds.Length
      )

      let tracksIdsToImport =
        potentialTracksIds
        |> List.shuffle
        |> List.take user.Settings.PlaylistSize
        |> List.map TrackId

      do! _targetPlaylistService.SaveTracksAsync queueMessage.TelegramId tracksIdsToImport

      (ChatId(queueMessage.TelegramId), "Playlist generated!")
      |> _bot.SendTextMessageAsync
      |> ignore
    }
