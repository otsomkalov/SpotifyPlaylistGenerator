namespace Database.Entities;

public class Settings
{
    public bool? IncludeLikedTracks { get; set; }

    public int PlaylistSize { get; set; }

    public bool RecommendationsEnabled { get; set; }
}

public class Preset
{
    public string Id { get; set; }

    public string Name { get; set; }

    public long UserId { get; set; }

    public Settings Settings { get; set; }

    public IEnumerable<IncludedPlaylist> IncludedPlaylists { get; set; } = Array.Empty<IncludedPlaylist>();

    public IEnumerable<ExcludedPlaylist> ExcludedPlaylists { get; set; } = Array.Empty<ExcludedPlaylist>();

    public IEnumerable<TargetedPlaylist> TargetedPlaylists { get; set; } = Array.Empty<TargetedPlaylist>();
}