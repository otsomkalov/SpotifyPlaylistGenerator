using Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Database;

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; init; }

    public DbSet<TargetPlaylist> TargetPlaylists { get; init; }
    public DbSet<SourcePlaylist> SourcePlaylists { get; init; }
    public DbSet<HistoryPlaylist> HistoryPlaylists { get; init; }
    public DbSet<TargetHistoryPlaylist> TargetHistoryPlaylists { get; init; }

    public AppDbContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .OwnsOne(u => u.Settings, settings =>
            {
                settings.Property(s => s.PlaylistSize)
                    .HasDefaultValue(20);
            });

        modelBuilder.Entity<Playlist>()
            .HasDiscriminator(p => p.PlaylistType)
            .HasValue<SourcePlaylist>(PlaylistType.Source)
            .HasValue<HistoryPlaylist>(PlaylistType.History)
            .HasValue<TargetHistoryPlaylist>(PlaylistType.TargetHistory)
            .HasValue<TargetPlaylist>(PlaylistType.Target);
    }
}