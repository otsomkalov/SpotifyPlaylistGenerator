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
}