namespace Database.Entities;

public class User
{
    public long Id { get; set; }

    public int CurrentPresetId { get; set; }

    public virtual Preset CurrentPreset { get; set; }
}