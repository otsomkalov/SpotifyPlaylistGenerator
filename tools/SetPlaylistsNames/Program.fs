open System.Threading.Tasks
open SpotifyAPI.Web
open Npgsql.FSharp

module Task =
  let map mapping task' =
    task {
      let! value = task'

      return mapping value
    }

  let bind (binder: 'a -> Task<'b>) (taskResult: Task<'a>) : Task<'b> =
    task {
      let! result = taskResult

      return! binder result
    }

module Spotify =
  let createClient token =
    TokenAuthenticator(token, "Bearer")
    |> SpotifyClientConfig.CreateDefault().WithAuthenticator
    |> SpotifyClient
    :> ISpotifyClient

  let getPlaylist (client: ISpotifyClient) =
    fun url ->
      task {
        try
          let request = PlaylistGetRequest()
          request.Fields.Add("name")

          let! playlist = client.Playlists.Get(url, request)

          return (playlist.Name, url)
        with _ ->
          return ("Not found", url)
      }

module DB =
  let loadPlaylistsUrls connectionString =
    fun () ->
      task {
        let! urls =
          connectionString
          |> Sql.connect
          |> Sql.query "SELECT * FROM \"spotify-playlist-generator\".public.\"Playlists\""
          |> Sql.executeAsync (fun read -> read.text "Url")
          |> Task.map List.distinct

        printfn "Loaded %i unique playlists urls from DB" (urls |> List.length)

        return urls
      }

  let updatePlaylistNameByUrl connectionString =
    fun (name, url) ->
      async {
        let! _ =
          connectionString
          |> Sql.connect
          |> Sql.query "update \"spotify-playlist-generator\".public.\"Playlists\" set \"Name\" = @name where \"Url\" = @url"
          |> Sql.parameters [ ("name", Sql.string name); ("url", Sql.string url) ]
          |> Sql.executeNonQueryAsync
          |> Async.AwaitTask

        printfn "Updated playlist with %s with name '%s' in DB" url name

        return ()
      }

let connectionString =
  "<PostgreSQL connection string>"
let token =
  "<Spotify access token>"

let loadPlaylistsUrls = DB.loadPlaylistsUrls connectionString
let updatePlaylistNameByUrl = DB.updatePlaylistNameByUrl connectionString
let client = Spotify.createClient token
let getSpotifyPlaylist = Spotify.getPlaylist client

loadPlaylistsUrls ()
|> Task.bind (Seq.map getSpotifyPlaylist >> Task.WhenAll)
|> Task.bind (Seq.map updatePlaylistNameByUrl >> Async.Sequential >> Async.StartAsTask)
|> Async.AwaitTask
|> Async.RunSynchronously
|> ignore

printfn "Playlists names update is done!"