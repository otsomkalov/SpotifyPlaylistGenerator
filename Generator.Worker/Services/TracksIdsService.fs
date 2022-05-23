module Generator.Worker.Services.TracksIdsService

open System.IO

let private refreshCachedAsync env filePath loadIdsFunc =
  task {
    let! ids = loadIdsFunc env

    do! FileService.saveIdsAsync filePath ids

    return ids
  }

let readOrDownloadAsync env idsFileName downloadIdsFunc refreshCache =
  match (refreshCache, File.Exists idsFileName) with
  | true, true -> refreshCachedAsync env idsFileName downloadIdsFunc
  | true, false -> refreshCachedAsync env idsFileName downloadIdsFunc
  | false, true -> FileService.readIdsAsync idsFileName
  | false, false -> refreshCachedAsync env idsFileName downloadIdsFunc
