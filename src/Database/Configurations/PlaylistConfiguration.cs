using Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Database.Configurations;

public class PlaylistConfiguration : IEntityTypeConfiguration<Playlist>
{
    public void Configure(EntityTypeBuilder<Playlist> builder)
    {
        builder
            .ToTable("Playlists")
            .HasDiscriminator(p => p.PlaylistType)
            .HasValue<SourcePlaylist>(PlaylistType.Source)
            .HasValue<HistoryPlaylist>(PlaylistType.History)
            .HasValue<TargetPlaylist>(PlaylistType.Target);
    }
}