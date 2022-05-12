namespace Shared.Data

open Microsoft.EntityFrameworkCore
open Microsoft.FSharp.Core

type PlaylistType =
  | Source = 0
  | History = 1
  | Target = 2
  | TargetHistory = 3

type User() =
  member val Id = 0L with get, set

  member val Playlists: Playlist list = [] with get, set

and Playlist() =
  member val Id = 0 with get, set

  member val PlaylistType: PlaylistType = PlaylistType.Source with get, set
  member val Url = "" with get, set

  member val UserId = 0L with get, set

  member val User = Unchecked.defaultof<User> with get, set

type AppDbContext(options: DbContextOptions) =
  inherit DbContext(options)

  member this.Users = this.Set<User>()
  member this.Playlists = this.Set<Playlist>()
