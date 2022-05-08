namespace Generator.Services


type TracksIdsService(_fileService: FileService) =
  let refreshCachedAsync filePath loadIdsFunc =
    task {
      let! ids = loadIdsFunc

      do! _fileService.SaveIdsAsync filePath ids

      return ids
    }

  member _.ReadOrDownloadAsync idsFileName downloadIdsFunc refreshCache =
    match (refreshCache, _fileService.Exists idsFileName) with
    | true, true -> refreshCachedAsync idsFileName downloadIdsFunc
    | true, false -> refreshCachedAsync idsFileName downloadIdsFunc
    | false, true -> _fileService.ReadIdsAsync idsFileName
    | false, false -> refreshCachedAsync idsFileName downloadIdsFunc
