namespace Infrastructure.Workflows

open Database
open Domain.Workflows
open Infrastructure.Core
open Infrastructure.Mapping
open Microsoft.EntityFrameworkCore
open System.Linq

module ValidateUserPlaylists =
  let loadUser (context: AppDbContext) : ValidateUserPlaylists.LoadUser =
    fun userId ->
      task {
        let rawUserId = (userId |> UserId.value)

        let! userPlaylists =
          context
            .Playlists
            .AsNoTracking()
            .Where(fun p -> p.UserId = rawUserId && p.Disabled = false)
            .ToListAsync()

        return User.fromDb userId userPlaylists
      }

[<RequireQualifiedAccess>]
module UserSettings =
  let load (context: AppDbContext) : UserSettings.Load =
    fun userId ->
      task{
        let rawUserId = (userId |> UserId.value)

        let! userSettings =
          context
            .Users
            .AsNoTracking()
            .Where(fun u -> u.Id = rawUserId)
            .Select(fun u -> u.Settings)
            .FirstOrDefaultAsync()

        return UserSettings.fromDb userSettings
      }

  let update (context:AppDbContext) : UserSettings.Update =
    fun settings ->
      task{
        let updatedUser = User.toDb
      }