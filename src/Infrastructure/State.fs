[<RequireQualifiedAccess>]
module Infrastructure.State

open System
open System.Threading.Tasks
open StackExchange.Redis
open Domain.Extensions

type StateKey = private StateKey of string

module StateKey =

  let create=
    Guid.NewGuid() |> string |> StateKey

  let parse (key: string) =
    key |> Guid.Parse |> string |> StateKey

  let value (StateKey value) = value |> string

type SetState = StateKey -> string -> Task<unit>

let setState (connectionMultiplexer: IConnectionMultiplexer) : SetState =
  let database = connectionMultiplexer.GetDatabase 2

  fun key value ->
    database.StringSetAsync((key |> StateKey.value), value, TimeSpan.FromMinutes(5))
    |> Task.map ignore

type GetState = StateKey -> Task<string option>

let getState (connectionMultiplexer: IConnectionMultiplexer) : GetState =
  let database = connectionMultiplexer.GetDatabase 2

  StateKey.value
  >> database.StringGetAsync
  >> Task.map (fun s -> if s.IsNullOrEmpty then None else Some s)
  >> Task.map (Option.map string)
