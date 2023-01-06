namespace Generator.Worker.Services

open System.Text.Json
open System.Threading.Tasks
open Generator.Worker.Domain
open Microsoft.Extensions.Caching.Distributed

type TracksIdsService(_cache: IDistributedCache) =
  let refreshCachedAsync key loadIdsFunc =
    task {
      let! ids = loadIdsFunc

      do! _cache.SetStringAsync(key, JsonSerializer.Serialize(ids |> List.map RawTrackId.value))

      return ids
    }

  member _.ReadOrDownloadAsync idsFileName downloadIdsFunc refreshCache =
    task{
      let! fromCache = _cache.GetStringAsync(idsFileName)

      return!
        match (refreshCache, not(isNull fromCache)) with
        | true, true -> refreshCachedAsync idsFileName downloadIdsFunc
        | true, false -> refreshCachedAsync idsFileName downloadIdsFunc
        | false, true -> JsonSerializer.Deserialize<string list>(fromCache) |> List.map RawTrackId.create |> Task.FromResult
        | false, false -> refreshCachedAsync idsFileName downloadIdsFunc
    }
