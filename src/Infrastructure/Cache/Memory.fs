module internal Infrastructure.Cache.Memory

open Domain.Repos

module UserRepo =
  let listLikedTracks (listLikedTracks: UserRepo.ListLikedTracks) : UserRepo.ListLikedTracks =
    let listLikedTracksLazy = lazy listLikedTracks()

    fun () ->
      listLikedTracksLazy.Value