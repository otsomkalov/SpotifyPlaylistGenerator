namespace Generator.Worker.Services

open System.IO

type TracksIdsService(_fileService: FileService) =
  let refreshCachedAsync filePath loadIdsFunc =
    task {
      let! ids = loadIdsFunc

      do! _fileService.SaveIdsAsync filePath ids

      return ids
    }

  member _.ReadOrDownloadAsync idsFileName downloadIdsFunc refreshCache =
    let idsFilePath =
      [|Path.GetTempPath(); idsFileName|]
      |> Path.Combine

    match (refreshCache, _fileService.Exists idsFilePath) with
    | true, true -> refreshCachedAsync idsFilePath downloadIdsFunc
    | true, false -> refreshCachedAsync idsFilePath downloadIdsFunc
    | false, true -> _fileService.ReadIdsAsync idsFilePath
    | false, false -> refreshCachedAsync idsFilePath downloadIdsFunc
