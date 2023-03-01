namespace Infrastructure.Workflows

open Database
open Database.Entities
open Domain.Core
open Domain.Workflows
open Infrastructure.Core
open Infrastructure.Mapping
open Microsoft.EntityFrameworkCore
open System.Linq
open Infrastructure.Helpers

[<RequireQualifiedAccess>]
module ValidateUserPlaylists =
  let loadUser (context: AppDbContext) : ValidateUserPlaylists.LoadUser =
    fun userId ->
      task {
        let rawUserId = (userId |> UserId.value)

        let! targetPlaylists =
          context.TargetPlaylists.AsNoTracking().Where(fun p -> p.UserId = rawUserId && p.Disabled = false).ToListAsync()

        let! sourcePlaylists =
          context
            .SourcePlaylists
            .AsNoTracking()
            .Where(fun p -> p.UserId = rawUserId && p.Disabled = false)
            .ToListAsync()

        return User.fromDb userId sourcePlaylists targetPlaylists
      }

[<RequireQualifiedAccess>]
module UserSettings =
  let load (context: AppDbContext) : UserSettings.Load =
    fun userId ->
      let userId = userId |> UserId.value

      context
        .Users
        .AsNoTracking()
        .Where(fun u -> u.Id = userId)
        .Select(fun u -> u.Settings)
        .SingleOrDefaultAsync()
      |> Task.map UserSettings.fromDb

  let update (context: AppDbContext) : UserSettings.Update =
    fun userId settings ->
      task {
        let settings = settings |> UserSettings.toDb

        context.Users.Update(Database.Entities.User(Id = (userId |> UserId.value), Settings = settings))
        |> ignore

        let! _ = context.SaveChangesAsync()

        return ()
      }
