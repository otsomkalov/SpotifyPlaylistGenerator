namespace Bot.Services

open System.Collections.Generic

type SpotifyIdProvider() =
  let _ids = Dictionary<int64, string>()

  member this.Get telegramId =
    if _ids.ContainsKey(telegramId) then
      _ids[telegramId]
    else
      null

  member this.Set telegramId spotifyId =
    if _ids.ContainsKey(telegramId) then
      ()
    else
      _ids.Add(telegramId, spotifyId)
