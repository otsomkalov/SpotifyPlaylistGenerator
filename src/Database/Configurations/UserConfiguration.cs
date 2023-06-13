using Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Database.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasMany(u => u.Presets)
            .WithOne(p => p.User)
            .HasForeignKey(p => p.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(u => u.CurrentPreset)
            .WithOne()
            .HasForeignKey<User>(u => u.CurrentPresetId)
            .IsRequired(false);

        builder.HasIndex(u => u.CurrentPresetId)
            .IsUnique(false);
    }
}