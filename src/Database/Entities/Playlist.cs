namespace Database.Entities;

public enum PlaylistType
{
    Source,
    History,
    Target
}

public class Playlist
{
    public int Id { get; init; }

    public virtual PlaylistType PlaylistType { get; init; }

    public string Url { get; set; }

    public long UserId { get; init; }

    public bool Disabled { get; set; }

    public virtual User User { get; set; }
}