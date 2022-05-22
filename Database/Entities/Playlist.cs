namespace Database.Entities;

public enum PlaylistType
{
    Source,
    History,
    Target,
    TargetHistory
}

public class Playlist
{
    public int Id { get; init; }

    public PlaylistType PlaylistType { get; init; }

    public string Url { get; init; }

    public long UserId { get; init; }

    public virtual User User { get; set; }
}