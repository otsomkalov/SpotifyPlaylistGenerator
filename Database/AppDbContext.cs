using Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Database;

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; init; }

    public DbSet<Playlist> Playlists { get; init; }

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
    }
}