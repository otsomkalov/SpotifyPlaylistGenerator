namespace Shared.Services

open System.Collections.Generic
open SpotifyAPI.Web

type SpotifyClientProvider() =
  let _clientsBySpotifyId =
    Dictionary<string, ISpotifyClient>()

  let _clientsByTelegramId =
    Dictionary<int64, ISpotifyClient>()

  member this.Get telegramId =
    _clientsByTelegramId[telegramId]

  member this.Get spotifyId =
    _clientsBySpotifyId[spotifyId]

  member this.SetClient(spotifyId: string, client: ISpotifyClient) =
    match _clientsBySpotifyId.ContainsKey(spotifyId) with
    | false -> (spotifyId, client) |> _clientsBySpotifyId.Add
    | true -> ()

  member this.SetClient(telegramId: int64, client: ISpotifyClient) =
    match _clientsByTelegramId.ContainsKey(telegramId) with
    | false -> (telegramId, client) |> _clientsByTelegramId.Add
    | true -> ()
