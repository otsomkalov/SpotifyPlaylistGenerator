using Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Database;

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; init; }

    public DbSet<Preset> Presets { get; set; }

    public DbSet<TargetPlaylist> TargetPlaylists { get; init; }
    public DbSet<SourcePlaylist> SourcePlaylists { get; init; }
    public DbSet<HistoryPlaylist> HistoryPlaylists { get; init; }

    public AppDbContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}