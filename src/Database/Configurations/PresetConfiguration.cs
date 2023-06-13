using Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Database.Configurations;

public class PresetConfiguration : IEntityTypeConfiguration<Preset>
{
    public void Configure(EntityTypeBuilder<Preset> builder)
    {
        builder.OwnsOne(p => p.Settings, settings =>
        {
            settings.Property(s => s.PlaylistSize)
                .HasDefaultValue(20);
        });
    }
}