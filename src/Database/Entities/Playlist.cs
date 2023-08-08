namespace Database.Entities;

public enum PlaylistType
{
    Source,
    History,
    Target
}

public abstract class Playlist
{
    public int Id { get; init; }

    public string Name { get; init; }

    public virtual PlaylistType PlaylistType { get; }

    public string Url { get; set; }

    public bool Disabled { get; set; }

    public virtual int PresetId { get; set; }

    public virtual Preset Preset { get; set; }
}

public class SourcePlaylist : Playlist
{
    public override PlaylistType PlaylistType => PlaylistType.Source;
}

public class HistoryPlaylist : Playlist
{
    public override PlaylistType PlaylistType => PlaylistType.History;
}

public class TargetPlaylist : Playlist
{
    public override PlaylistType PlaylistType => PlaylistType.Target;

    public bool Overwrite { get; set; }
}