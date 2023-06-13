namespace Database.Entities;

public class Settings
{
    public bool? IncludeLikedTracks { get; set; }

    public int PlaylistSize { get; set; }
}

public class Preset
{
    public int Id { get; set; }

    public string Name { get; set; }

    public virtual long UserId { get; set; }

    public Settings Settings { get; set; }

    public virtual User User { get; set; }
}