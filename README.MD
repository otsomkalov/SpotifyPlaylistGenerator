# SpotifyPlaylistGenerator

Generate playlist with unique tracks by exporting them from other playlists

## How it works

1. Loads list of liked tracks
2. Loads list of already listened tracks from playlist
3. Loads list of tracks from playlists with ids set in configuration
4. Clean tracks from last step from tracks loaded at steps 1 and 2
5. Shuffle tracks
6. Select only 100
7. Save to target playlist and update playlist with already listened tracks

## How to run

1. Update **appsettings.json**
2. Run `dotnet run` command in project directory