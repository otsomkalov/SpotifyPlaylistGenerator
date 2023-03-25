module Infrastructure.Cache

open System
open System.Threading.Tasks
open Infrastructure.Helpers
open StackExchange.Redis

type CacheData<'k> = 'k -> string list -> Task<unit>

let private cacheData (cache: IDatabase) : CacheData<'k> =
  fun key values ->
    task {
      let values = values |> List.map (string >> RedisValue) |> Array.ofSeq
      let key = RedisKey(key |> string)
      let! _ = cache.ListLeftPushAsync(key, values)
      let! _ = cache.KeyExpireAsync(key, TimeSpan.FromDays(7))

      return ()
    }

type ListDataFunc<'k> = 'k -> Task<string list>
type LoadAndCacheData<'k> = 'k -> Task<string list>

let private loadAndCacheData<'k> loadData cacheData : LoadAndCacheData<'k> =
  fun id ->
    task {
      let! data = loadData id

      do! cacheData id data

      return data
    }

type TryListByKey<'k> = 'k -> Task<string list>

let private tryListByKey<'k> (cache: IDatabase) loadAndCacheData : TryListByKey<'k> =
  fun key ->
    task {
      let! values = key |> string |> cache.ListRangeAsync

      return!
        match values with
        | [||] -> loadAndCacheData key
        | v -> v |> List.ofSeq |> List.map string |> Task.FromResult
    }

type ListOrRefresh<'k> = 'k -> Task<string list>

let listOrRefresh (cache: IDatabase) loadData refreshCache : ListOrRefresh<'k> =
  if refreshCache then
    loadAndCacheData loadData (cacheData cache)
  else
    tryListByKey cache (loadAndCacheData loadData (cacheData cache))
