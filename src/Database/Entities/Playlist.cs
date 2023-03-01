namespace Database.Entities;

public enum PlaylistType
{
    Source,
    History,
    Target,
    TargetHistory
}

public abstract class Playlist
{
    public int Id { get; init; }

    public virtual PlaylistType PlaylistType { get; init; }

    public string Url { get; set; }

    public long UserId { get; init; }

    public bool Disabled { get; set; }

    public virtual User User { get; set; }
}

public class SourcePlaylist : Playlist
{
    public override PlaylistType PlaylistType { get; init; } = PlaylistType.Source;
}

public class HistoryPlaylist : Playlist
{
    public override PlaylistType PlaylistType { get; init; } = PlaylistType.History;
}

public class TargetHistoryPlaylist : Playlist
{
    public override PlaylistType PlaylistType { get; init; } = PlaylistType.TargetHistory;
}

public class TargetPlaylist : Playlist
{
    public override PlaylistType PlaylistType { get; init; } = PlaylistType.Target;

    public bool Overwrite { get; set; }
}