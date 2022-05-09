namespace Shared.Services

open System.Collections.Generic
open SpotifyAPI.Web

type SpotifyClientProvider() =
  let _clientsBySpotifyId =
    Dictionary<string, ISpotifyClient>()

  let _clientsByTelegramId =
    Dictionary<int64, ISpotifyClient>()

  member this.GetClient telegramId =
    if _clientsByTelegramId.ContainsKey(telegramId) then
      _clientsByTelegramId[telegramId]
    else
      null

  member this.GetClient spotifyId =
    if _clientsBySpotifyId.ContainsKey(spotifyId) then
      _clientsBySpotifyId[spotifyId]
    else
      null

  member this.SetClient(spotifyId: string, client: ISpotifyClient) =
    if _clientsBySpotifyId.ContainsKey(spotifyId) then
      ()
    else
      (spotifyId, client) |> _clientsBySpotifyId.Add

  member this.SetClient(telegramId: int64, client: ISpotifyClient) =
    if _clientsByTelegramId.ContainsKey(telegramId) then
      ()
    else
      (telegramId, client) |> _clientsByTelegramId.Add
