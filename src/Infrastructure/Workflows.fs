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
        let! userPlaylists =
          context
            .Playlists
            .AsNoTracking()
            .Where(fun p -> p.UserId = (userId |> UserId.value) && p.Disabled = false)
            .ToListAsync()

        return User.fromDb userId userPlaylists
      }
