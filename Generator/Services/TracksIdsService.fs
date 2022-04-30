namespace Generator.Services

open Generator.Settings
open Microsoft.Extensions.Options

type TracksIdsService(_fileService: FileService, _options: IOptions<Settings>) =
    let _settings = _options.Value

    let refreshCachedAsync filePath loadIdsFunc =
        task {
            let! ids = loadIdsFunc

            do! _fileService.saveIdsAsync filePath ids

            return ids
        }

    member _.readOrDownloadAsync idsFileName downloadIdsFunc =
        match (_settings.RefreshCache, _fileService.exists idsFileName) with
        | true, true -> refreshCachedAsync idsFileName downloadIdsFunc
        | true, false -> refreshCachedAsync idsFileName downloadIdsFunc
        | false, true -> _fileService.readIdsAsync idsFileName
        | false, false -> refreshCachedAsync idsFileName downloadIdsFunc
