namespace Database.Entities;

public class TargetPlaylist : Playlist
{
    public override PlaylistType PlaylistType => PlaylistType.Target;

    public bool Overwrite { get; set; }
}