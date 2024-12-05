module MusicPlatform.Spotify.Startup

open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open MusicPlatform

let addSpotifyMusicPlatform (cfg: IConfiguration) (service: IServiceCollection) =
  service.AddSingleton<Playlist.ParseId>(Playlist.parseId)