module internal Infrastructure.Cache.Memory

open System.Threading
open System.Threading.Tasks
open Domain.Core
open Domain.Repos

module UserRepo =
  let listLikedTracks (listLikedTracks: UserRepo.ListLikedTracks): UserRepo.ListLikedTracks =
    let mutable tracks : Track list option = None
    let semaphore = new SemaphoreSlim(1,1)

    fun () ->
      match tracks with
      | None ->
        task {
          do! semaphore.WaitAsync()

          return!
            match tracks with
            | Some t ->
              semaphore.Release() |> ignore

              t |> Task.FromResult
            | None ->
              task {
                let! likedTracks = listLikedTracks()

                tracks <- Some likedTracks

                semaphore.Release() |> ignore

                return likedTracks
              }
        }
      | Some t -> Task.FromResult t