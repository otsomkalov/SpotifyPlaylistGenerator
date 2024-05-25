using MongoDB.Bson.Serialization.Attributes;

namespace Database.Entities;

[BsonIgnoreExtraElements]
public abstract class Playlist
{
    public string Id { get; init; }

    public string Name { get; set; }

    public bool Disabled { get; set; }
}

public class IncludedPlaylist : Playlist
{
}

public class ExcludedPlaylist : Playlist
{
}

public class TargetedPlaylist : Playlist
{
    public bool Overwrite { get; set; }
}