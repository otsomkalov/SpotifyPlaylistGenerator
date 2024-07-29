using MongoDB.Bson.Serialization.Attributes;

namespace Database.Entities;

[BsonIgnoreExtraElements]
public class User
{
    public long Id { get; set; }

    public string CurrentPresetId { get; set; }

    public IEnumerable<SimplePreset> Presets { get; set; }
}