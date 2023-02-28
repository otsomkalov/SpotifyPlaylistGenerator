namespace Database.Entities;

public class Settings
{
    public bool? IncludeLikedTracks { get; set; }

    public int PlaylistSize { get; set; }
}

public class User
{
    public long Id { get; set; }

    public Settings Settings { get; set; }

    public virtual IEnumerable<Playlist> Playlists { get; set; }
}