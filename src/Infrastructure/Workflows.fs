namespace Infrastructure.Workflows

open System
open System.Collections.Generic
open System.Threading.Tasks
open Database.Entities
open SpotifyAPI.Web
open Infrastructure
open System.Net
open System.Text.RegularExpressions
open Database
open Domain.Core
open Domain.Workflows
open Infrastructure.Core
open Infrastructure.Mapping
open Microsoft.EntityFrameworkCore
open System.Linq
open Infrastructure.Helpers
open Microsoft.Extensions.Logging
open StackExchange.Redis
open Infrastructure.Helpers.Spotify
open Domain.Extensions

[<RequireQualifiedAccess>]
module PresetSettings =
  let load (context: AppDbContext) : PresetSettings.Load =
    let loadFromDb userId =
      context.Users
        .AsNoTracking()
        .Include(fun u -> u.CurrentPreset)
        .ThenInclude(fun p -> p.Settings)
        .Where(fun u -> u.Id = userId)
        .Select(fun u -> u.CurrentPreset.Settings)
        .FirstOrDefaultAsync()

    UserId.value >> loadFromDb >> Task.map PresetSettings.fromDb

  let update (context: AppDbContext) : PresetSettings.Update =
    fun userId settings ->
      task {
        let userId = userId |> UserId.value

        let! dbPreset =
          context.Users
            .Include(fun u -> u.CurrentPreset)
            .ThenInclude(fun p -> p.Settings)
            .Where(fun u -> u.Id = userId)
            .Select(fun u -> u.CurrentPreset)
            .FirstOrDefaultAsync()

        let updatedDbSettings = settings |> PresetSettings.toDb

        dbPreset.Settings <- updatedDbSettings

        context.Presets.Update(dbPreset) |> ignore

        let! _ = context.SaveChangesAsync()

        return ()
      }

[<RequireQualifiedAccess>]
module User =
  let rec private listLikedTracks' (client: ISpotifyClient) (offset: int) =
    async {
      let! tracks =
        client.Library.GetTracks(LibraryTracksRequest(Offset = offset, Limit = 50))
        |> Async.AwaitTask

      let! nextTracksIds =
        if isNull tracks.Next then
          [] |> async.Return
        else
          listLikedTracks' client (offset + 50)

      let currentTracksIds =
        tracks.Items |> Seq.map (fun x -> x.Track) |> Spotify.getTracksIds

      return List.append nextTracksIds currentTracksIds
    }

  let listLikedTracks (client: ISpotifyClient) : User.ListLikedTracks = listLikedTracks' client 0

  let loadCurrentPreset (context: AppDbContext) : User.LoadCurrentPreset =
    fun userId ->
      let userId = userId |> UserId.value

      context.Users
        .AsNoTracking()
        .Include(fun u -> u.CurrentPreset)
        .ThenInclude(fun x -> x.SourcePlaylists)
        .Include(fun u -> u.CurrentPreset)
        .ThenInclude(fun x -> x.HistoryPlaylists)
        .Include(fun u -> u.CurrentPreset)
        .ThenInclude(fun x -> x.TargetPlaylists)
        .Where(fun u -> u.Id = userId)
        .Select(fun u -> u.CurrentPreset)
        .FirstOrDefaultAsync()
      |> Async.AwaitTask
      |> Async.map Preset.fromDb

  let listPresets (context: AppDbContext) : User.ListPresets =
    let listPresets userId =
      context.Presets.AsNoTracking().Where(fun p -> p.UserId = userId).ToListAsync()

    UserId.value
    >> listPresets
    >> Task.map (Seq.map SimplePreset.fromDb)
    >> Async.AwaitTask

  let loadPreset (context: AppDbContext) : User.LoadPreset =
    let loadPreset presetId =
      context.Presets.AsNoTracking().FirstOrDefaultAsync(fun p -> p.Id = presetId)

    PresetId.value >> loadPreset >> Task.map SimplePreset.fromDb >> Async.AwaitTask

  let getCurrentPresetId (context: AppDbContext) : User.GetCurrentPresetId =
    fun userId ->
      let userId = userId |> UserId.value

      context.Users
        .AsNoTracking()
        .Where(fun u -> u.Id = userId)
        .Select(fun u -> u.CurrentPresetId)
        .FirstOrDefaultAsync()
      |> Task.map (fun id -> id.Value |> PresetId)
      |> Async.AwaitTask

[<RequireQualifiedAccess>]
module TargetPlaylist =
  let updateTracks (cache: IDatabase) (client: ISpotifyClient) : Playlist.UpdateTracks =
    fun playlist tracksIds ->
      let tracksIds = tracksIds |> List.map TrackId.value
      let playlistId = playlist.Id |> WritablePlaylistId.value |> PlaylistId.value

      let spotifyTracksIds =
        tracksIds |> List.map (fun id -> $"spotify:track:{id}") |> List<string>

      if playlist.Overwrite then
        task {

          let transaction = cache.CreateTransaction()

          let deleteTask = transaction.KeyDeleteAsync(playlistId) :> Task

          let addTask =
            transaction.ListLeftPushAsync(playlistId, (tracksIds |> List.map RedisValue |> Seq.toArray)) :> Task

          let expireTask = transaction.KeyExpireAsync(playlistId, TimeSpan.FromDays(7))

          let! _ = transaction.ExecuteAsync()

          let! _ = deleteTask
          let! _ = addTask
          let! _ = expireTask

          let! _ = client.Playlists.ReplaceItems(playlistId, PlaylistReplaceItemsRequest spotifyTracksIds)

          ()
        }
        |> Async.AwaitTask
      else
        let playlistAddItemsRequest = spotifyTracksIds |> PlaylistAddItemsRequest

        [ cache.ListLeftPushAsync(playlistId, (tracksIds |> List.map RedisValue |> Seq.toArray)) :> Task
          client.Playlists.AddItems(playlistId, playlistAddItemsRequest) :> Task ]
        |> Task.WhenAll
        |> Async.AwaitTask

  let overwriteTargetPlaylist (context: AppDbContext) : TargetPlaylist.OverwriteTracks =
    fun presetId targetPlaylistId ->
      task {
        let targetPlaylistId =
          targetPlaylistId |> WritablePlaylistId.value |> PlaylistId.value

        let presetId = presetId |> PresetId.value

        let! targetPlaylist =
          context.TargetPlaylists
            .Where(fun p -> p.PresetId = presetId && p.Url = targetPlaylistId)
            .FirstOrDefaultAsync()

        targetPlaylist.Overwrite <- true

        context.Update(targetPlaylist) |> ignore

        let! _ = context.SaveChangesAsync()

        return ()
      }

  let appendToTargetPlaylist (context: AppDbContext) : TargetPlaylist.AppendTracks =
    fun presetId targetPlaylistId ->
      task {
        let targetPlaylistId =
          targetPlaylistId |> WritablePlaylistId.value |> PlaylistId.value

        let presetId = presetId |> PresetId.value

        let! targetPlaylist =
          context.TargetPlaylists
            .Where(fun p -> p.PresetId = presetId && p.Url = targetPlaylistId)
            .FirstOrDefaultAsync()

        targetPlaylist.Overwrite <- false

        context.Update(targetPlaylist) |> ignore

        let! _ = context.SaveChangesAsync()

        return ()
      }

  let remove (context: AppDbContext) : TargetPlaylist.Remove =
    fun presetId targetPlaylistId ->
      task{
        let presetId = presetId |> PresetId.value
        let playlistId = targetPlaylistId |> WritablePlaylistId.value |> PlaylistId.value

        let! dbPlaylist = context.TargetPlaylists.FirstOrDefaultAsync(fun tp -> tp.PresetId = presetId && tp.Url = playlistId)

        do context.Remove dbPlaylist |> ignore

        return! context.SaveChangesAsync() |> Task.map ignore
      }

  let update (context: AppDbContext) : TargetPlaylist.Update =
    fun presetId targetPlaylist ->
      let presetId = presetId |> PresetId.value
      let targetPlaylistId = targetPlaylist.Id |> WritablePlaylistId.value |> PlaylistId.value

      task{
        let! dbPlaylist = context.TargetPlaylists.FirstOrDefaultAsync(fun tp -> tp.Url = targetPlaylistId && tp.PresetId = presetId)

        dbPlaylist.Name <- targetPlaylist.Name
        dbPlaylist.Overwrite <- targetPlaylist.Overwrite

        do context.Update(dbPlaylist) |> ignore

        return! context.SaveChangesAsync() |> Task.map ignore
      }

[<RequireQualifiedAccess>]
module Playlist =
  let rec private listTracks' (client: ISpotifyClient) playlistId (offset: int) =
    async {
      let! tracks =
        client.Playlists.GetItems(playlistId, PlaylistGetItemsRequest(Offset = offset))
        |> Async.AwaitTask

      let! nextTracksIds =
        if isNull tracks.Next then
          [] |> async.Return
        else
          listTracks' client playlistId (offset + 100)

      let currentTracksIds =
        tracks.Items |> Seq.map (fun x -> x.Track :?> FullTrack) |> Spotify.getTracksIds

      return List.append nextTracksIds currentTracksIds
    }

  let listTracks (logger: ILogger) client : Playlist.ListTracks =
    fun playlistId ->
      async {
        try
          let playlistId = playlistId |> ReadablePlaylistId.value |> PlaylistId.value

          return! listTracks' client playlistId 0
        with ApiException e when e.Response.StatusCode = HttpStatusCode.NotFound ->
          logger.LogInformation(
            "Playlist with id {PlaylistId} not found in Spotify",
            playlistId |> ReadablePlaylistId.value |> PlaylistId.value
          )

          return []
      }

  let parseId: Playlist.ParseId =
    fun rawPlaylistId ->
      let getPlaylistIdFromUri (uri: Uri) = uri.Segments |> Array.last

      let (|Uri|_|) text =
        match Uri.TryCreate(text, UriKind.Absolute) with
        | true, uri -> Some uri
        | _ -> None

      let (|PlaylistId|_|) text =
        if Regex.IsMatch(text, "[A-z0-9]{22}") then
          Some text
        else
          None

      let (|SpotifyUri|_|) (text: string) =
        match text.Split(":") with
        | [| "spotify"; "playlist"; id |] -> Some(id)
        | _ -> None

      match rawPlaylistId |> RawPlaylistId.value with
      | SpotifyUri id -> id |> Playlist.ParsedPlaylistId |> Ok
      | Uri uri -> uri |> getPlaylistIdFromUri |> Playlist.ParsedPlaylistId |> Ok
      | PlaylistId id -> id |> Playlist.ParsedPlaylistId |> Ok
      | _ -> Playlist.IdParsingError() |> Error

  let checkPlaylistExistsInSpotify (client: ISpotifyClient) : Playlist.CheckExistsInSpotify =
    fun playlistId ->
      let rawPlaylistId = playlistId |> ParsedPlaylistId.value

      async {
        try
          let! playlist = rawPlaylistId |> client.Playlists.Get |> Async.AwaitTask

          return
            { Id = playlist.Id |> PlaylistId
              Name = playlist.Name
              OwnerId = playlist.Owner.Id }
            |> Ok
        with ApiException e when e.Response.StatusCode = HttpStatusCode.NotFound ->
          return Playlist.MissingFromSpotifyError rawPlaylistId |> Error
      }

  let checkWriteAccess (client: ISpotifyClient) : Playlist.CheckWriteAccess =
    fun playlist ->
      async {
        let! currentUser = client.UserProfile.Current() |> Async.AwaitTask

        return
          if playlist.OwnerId = currentUser.Id then
            playlist |> WritablePlaylist.fromSpotifyPlaylist |> Ok
          else
            Playlist.AccessError() |> Error
      }

  let includeInStorage (context: AppDbContext) userId (loadCurrentPreset: User.LoadCurrentPreset) : Playlist.IncludeInStorage =
    fun playlist ->
      async {
        let! currentPreset = loadCurrentPreset userId

        let! _ =
          SourcePlaylist(
            Name = playlist.Name,
            Url = (playlist.Id |> ReadablePlaylistId.value |> PlaylistId.value),
            PresetId = (currentPreset.Id |> PresetId.value)
          )
          |> context.SourcePlaylists.AddAsync
          |> ValueTask.asTask
          |> Async.AwaitTask

        let! _ = context.SaveChangesAsync() |> Async.AwaitTask

        return playlist
      }

  let excludeInStorage (context: AppDbContext) userId (loadCurrentPreset: User.LoadCurrentPreset) : Playlist.ExcludeInStorage =
    fun playlist ->
      task {
        let! currentPreset = loadCurrentPreset userId

        let! _ =
          HistoryPlaylist(
            Name = playlist.Name,
            Url = (playlist.Id |> ReadablePlaylistId.value |> PlaylistId.value),
            PresetId = (currentPreset.Id |> PresetId.value)
          )
          |> context.HistoryPlaylists.AddAsync

        let! _ = context.SaveChangesAsync()

        return playlist
      } |> Async.AwaitTask

  let targetInStorage (context: AppDbContext) userId : Playlist.TargetInStorage =
    fun playlist ->
      async {
        let! currentPreset = User.loadCurrentPreset context userId

        let! _ =
          TargetPlaylist(
            Name = playlist.Name,
            Url = (playlist.Id |> WritablePlaylistId.value |> PlaylistId.value),
            PresetId = (currentPreset.Id |> PresetId.value)
          )
          |> context.TargetPlaylists.AddAsync
          |> ValueTask.asTask
          |> Async.AwaitTask

        let! _ = context.SaveChangesAsync() |> Async.AwaitTask

        return playlist
      }

  let countTracks (connectionMultiplexer: IConnectionMultiplexer) : Playlist.CountTracks =
    let database = connectionMultiplexer.GetDatabase 0
    PlaylistId.value
    >> RedisKey
    >> database.ListLengthAsync

[<RequireQualifiedAccess>]
module Preset =
  let load (context: AppDbContext) : Preset.Load =
    let loadDbPreset presetId =
      context.Presets.AsNoTracking()
        .AsNoTracking()
        .Include(fun x -> x.SourcePlaylists)
        .Include(fun x -> x.HistoryPlaylists)
        .Include(fun x -> x.TargetPlaylists)
        .FirstOrDefaultAsync(fun p -> p.Id = presetId)
      |> Async.AwaitTask

    PresetId.value >> loadDbPreset >> Async.map Preset.fromDb

  let updateSettings (context: AppDbContext) (presetId: PresetId) : Preset.UpdateSettings =
    fun settings ->
      task {
        let (PresetId presetId) = presetId

        let! dbPreset =
          context.Presets
            .FirstOrDefaultAsync(fun p -> p.Id = presetId)

        let updatedDbSettings = settings |> PresetSettings.toDb

        dbPreset.Settings <- updatedDbSettings

        context.Presets.Update(dbPreset) |> ignore

        let! _ = context.SaveChangesAsync()

        return ()
      }

  let setLikedTracksHandling (loadPreset: Preset.Load) (updateSettings: Preset.UpdateSettings) : Preset.SetLikedTracksHandling =
    fun presetId likedTracksHandling ->
      loadPreset presetId
      |> Async.StartAsTask
      |> Task.map (fun p ->
        { p.Settings with
            LikedTracksHandling = likedTracksHandling })
      |> Task.bind updateSettings

[<RequireQualifiedAccess>]
module IncludedPlaylist =
  let enable (context: AppDbContext) : IncludedPlaylist.Enable =
    fun presetId playlistId ->
      let presetId = presetId |> PresetId.value
      let playlistId = playlistId |> ReadablePlaylistId.value |> PlaylistId.value

      task{
        let! dbPlaylist = context.SourcePlaylists.FirstOrDefaultAsync(fun tp -> tp.Url = playlistId && tp.PresetId = presetId)

        dbPlaylist.Disabled <- false

        do dbPlaylist |> context.Update |> ignore

        return! context.SaveChangesAsync() |> Task.map ignore
      }

  let disable (context: AppDbContext) : IncludedPlaylist.Disable =
    fun presetId playlistId ->
      let presetId = presetId |> PresetId.value
      let playlistId = playlistId |> ReadablePlaylistId.value |> PlaylistId.value

      task{
        let! dbPlaylist = context.SourcePlaylists.FirstOrDefaultAsync(fun tp -> tp.Url = playlistId && tp.PresetId = presetId)

        dbPlaylist.Disabled <- true

        do dbPlaylist |> context.Update |> ignore

        return! context.SaveChangesAsync() |> Task.map ignore
      }