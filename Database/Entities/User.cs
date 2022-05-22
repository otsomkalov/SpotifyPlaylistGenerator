namespace Database.Entities;

public class User
{
    public long Id { get; set; }

    public virtual IEnumerable<Playlist> Playlists { get; set; }
}