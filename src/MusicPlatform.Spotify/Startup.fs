module MusicPlatform.Spotify.Startup

open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open MusicPlatform
open MusicPlatform.Spotify.Core
open otsom.fs.Extensions.DependencyInjection

let addSpotifyMusicPlatform (cfg: IConfiguration) (services: IServiceCollection) =
  services.BuildSingleton<GetClient, _, _>(getClient)

  services.BuildSingleton<BuildMusicPlatform, _>(Library.buildMusicPlatform)