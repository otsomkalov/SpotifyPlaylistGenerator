namespace Generator.Services

open SpotifyAPI.Web

type SpotifyClientProvider() =
    let mutable _client: ISpotifyClient = null

    member _.Client = _client

    member _.setClient(client: ISpotifyClient) = _client <- client
