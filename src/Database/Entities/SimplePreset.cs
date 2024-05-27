using MongoDB.Bson.Serialization.Attributes;

namespace Database.Entities;

[BsonIgnoreExtraElements]
public class SimplePreset
{
    public string Id { get; set; }

    public string Name { get; set; }
}