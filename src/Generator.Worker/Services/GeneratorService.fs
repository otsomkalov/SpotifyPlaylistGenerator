namespace Generator.Worker.Services


open Database
open Domain.Core
open Domain.Workflows
open Infrastructure.Workflows
open Microsoft.Extensions.Logging
open Shared.QueueMessages
open Generator.Worker.Extensions
open Telegram.Bot
open Telegram.Bot.Types
open Microsoft.EntityFrameworkCore
open Infrastructure.Helpers
open System.Linq

type GeneratorService
  (
    _targetPlaylistService: TargetPlaylistService,
    _logger: ILogger<GeneratorService>,
    _bot: ITelegramBotClient,
    _context: AppDbContext
  ) =

  member this.GeneratePlaylistAsync
    (
      queueMessage: GeneratePlaylistMessage,
      listPlaylistTracks: Playlist.ListTracks,
      listLikedTracks: User.ListLikedTracks
    ) =
    async {
      _logger.LogInformation("Received request to generate playlist for user with Telegram id {TelegramId}", queueMessage.TelegramId)

      (ChatId(queueMessage.TelegramId), "Generating playlist...")
      |> _bot.SendTextMessageAsync
      |> ignore

      let! user =
        _context
          .Users
          .AsNoTracking()
          .Include(fun x -> x.SourcePlaylists.Where(fun p -> not p.Disabled))
          .Include(fun x -> x.HistoryPlaylists.Where(fun p -> not p.Disabled))
          .FirstOrDefaultAsync(fun u -> u.Id = queueMessage.TelegramId)
          |> Async.AwaitTask

      let! likedTracks = listLikedTracks

      let! includedTracks =
        user.SourcePlaylists
        |> Seq.map (fun p -> listPlaylistTracks p.Url)
        |> Async.Parallel
        |> Async.map List.concat

      let! excludedTracks =
        user.HistoryPlaylists
        |> Seq.map (fun p -> listPlaylistTracks p.Url)
        |> Async.Parallel
        |> Async.map List.concat

      let excludedTracksIds, includedTracksIds =
        match user.Settings.IncludeLikedTracks |> Option.ofNullable with
        | Some v when v = true -> excludedTracks, includedTracks @ likedTracks
        | Some v when v = false -> likedTracks @ excludedTracks, includedTracks
        | None -> excludedTracks, includedTracks

      _logger.LogInformation(
        "User with Telegram id {TelegramId} has {TracksToExcludeCount} tracks to exclude",
        queueMessage.TelegramId,
        excludedTracksIds.Length
      )

      let potentialTracksIds = includedTracksIds |> List.except excludedTracksIds

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
