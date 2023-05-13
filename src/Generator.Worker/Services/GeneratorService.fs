namespace Generator.Worker.Services


open Domain.Core
open Domain.Workflows
open Infrastructure.Core
open Infrastructure.Workflows
open Microsoft.Extensions.Logging
open Shared.QueueMessages
open Generator.Worker.Extensions
open Telegram.Bot
open Telegram.Bot.Types
open Domain.Extensions

type GeneratorService(_logger: ILogger<GeneratorService>, _bot: ITelegramBotClient) =

  member this.GeneratePlaylistAsync
    (
      queueMessage: GeneratePlaylistMessage,
      listPlaylistTracks: Playlist.ListTracks,
      listLikedTracks: User.ListLikedTracks,
      loadUser: User.Load,
      updateTargetPlaylist: Playlist.Update
    ) =
    async {
      _logger.LogInformation("Received request to generate playlist for user with Telegram id {TelegramId}", queueMessage.TelegramId)

      (ChatId(queueMessage.TelegramId), "Generating playlist...")
      |> _bot.SendTextMessageAsync
      |> ignore

      let userId = queueMessage.TelegramId |> UserId

      let! user = loadUser userId

      let! likedTracks = listLikedTracks

      let! includedTracks =
        user.IncludedPlaylists
        |> Seq.map listPlaylistTracks
        |> Async.Parallel
        |> Async.map List.concat

      let! excludedTracks =
        user.ExcludedPlaylist
        |> Seq.map listPlaylistTracks
        |> Async.Parallel
        |> Async.map List.concat

      let excludedTracksIds, includedTracksIds =
        match user.Settings.LikedTracksHandling with
        | UserSettings.LikedTracksHandling.Include -> excludedTracks, includedTracks @ likedTracks
        | UserSettings.LikedTracksHandling.Exclude -> likedTracks @ excludedTracks, includedTracks
        | UserSettings.LikedTracksHandling.Ignore -> excludedTracks, includedTracks

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
        |> List.take (user.Settings.PlaylistSize |> PlaylistSize.value)
        |> List.map TrackId

      for playlist in user.TargetPlaylists do
        do! updateTargetPlaylist playlist tracksIdsToImport

      (ChatId(queueMessage.TelegramId), "Playlist generated!")
      |> _bot.SendTextMessageAsync
      |> ignore
    }
