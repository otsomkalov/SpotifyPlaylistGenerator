namespace Database.Entities;

public class User
{
    public long Id { get; set; }

    public int? CurrentPresetId { get; set; }

    public virtual Preset? CurrentPreset { get; set; }

    public virtual IEnumerable<Preset> Presets { get; set; }

    public virtual IEnumerable<SourcePlaylist> SourcePlaylists { get; set; }
    public virtual IEnumerable<HistoryPlaylist> HistoryPlaylists { get; set; }
    public virtual IEnumerable<TargetPlaylist> TargetPlaylists { get; set; }
}