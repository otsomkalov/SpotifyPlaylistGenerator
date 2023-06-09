using Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Database.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder
            .OwnsOne(u => u.Settings, settings =>
            {
                settings.Property(s => s.PlaylistSize)
                    .HasDefaultValue(20);
            });
    }
}