module Generator.Worker.Services.GeneratorService

open System.Text.Json
open Generator.Worker
open Shared
open Shared.QueueMessages
open Generator.Worker.Extensions
open Shared.Spotify

let generatePlaylistAsync env (messageBody: string) =
  task {
    let queueMessage =
      JsonSerializer.Deserialize<GeneratePlaylistMessage>(messageBody)

    Log.infoWithArg env "Received request to generate playlist for user with Telegram id {TelegramId}" queueMessage.TelegramId

    do! Bot.sendMessage env queueMessage.TelegramId "Generating playlist..."
    let! user = Db.getUser env queueMessage.TelegramId

    let! likedTracksIds = LikedTracksService.listIdsAsync env queueMessage.TelegramId queueMessage.RefreshCache
    let! historyTracksIds = HistoryPlaylistsService.listTracksIdsAsync env queueMessage.TelegramId queueMessage.RefreshCache
    let! playlistsTracksIds = PlaylistsService.listTracksIdsAsync env queueMessage.TelegramId queueMessage.RefreshCache

    let excludedTracksIds, includedTracksIds =
      match user.Settings.IncludeLikedTracks with
      | true -> historyTracksIds, playlistsTracksIds @ likedTracksIds
      | false -> likedTracksIds @ historyTracksIds, playlistsTracksIds

    Log.infoWithArgs
      env
      "User with Telegram id {TelegramId} has {TracksToExcludeCount} tracks to exclude"
      queueMessage.TelegramId
      excludedTracksIds.Length

    let potentialTracksIds =
      includedTracksIds |> List.except excludedTracksIds

    Log.infoWithArgs
      env
      "User with Telegram id {TelegramId} has {PotentialTracksCount} potential tracks"
      queueMessage.TelegramId
      potentialTracksIds.Length

    let tracksIdsToImport =
      potentialTracksIds
      |> List.shuffle
      |> List.take user.Settings.PlaylistSize
      |> List.map SpotifyTrackId.create

    do! TargetPlaylistService.saveTracksAsync env queueMessage.TelegramId tracksIdsToImport
    do! HistoryPlaylistsService.updateAsync env queueMessage.TelegramId tracksIdsToImport

    let newHistoryTracksIds =
      tracksIdsToImport
      |> List.map SpotifyTrackId.rawValue
      |> List.append historyTracksIds

    do! HistoryPlaylistsService.updateCachedAsync env queueMessage.TelegramId newHistoryTracksIds
    do! Bot.sendMessage env queueMessage.TelegramId "Playlist generated!"
  }
