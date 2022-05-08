namespace Generator.Services

open System.Text.Json
open Shared.QueueMessages
open Shared.Services

type AccountsService(_spotifyClientProvider: SpotifyClientProvider) =
  member this.LinkAsync(message: string) =
    task {
      let linkAccountsMessage =
        JsonSerializer.Deserialize<LinkAccountsMessage>(message)

      let spotifyClient =
        _spotifyClientProvider.GetClient linkAccountsMessage.SpotifyId

      _spotifyClientProvider.SetClient(linkAccountsMessage.TelegramId, spotifyClient)

      return ()
    }
