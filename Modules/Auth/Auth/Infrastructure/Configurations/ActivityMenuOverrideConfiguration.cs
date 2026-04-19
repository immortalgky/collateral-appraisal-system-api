using Auth.Domain.Menu;

namespace Auth.Infrastructure.Configurations;

public class ActivityMenuOverrideConfiguration : IEntityTypeConfiguration<ActivityMenuOverride>
{
    public void Configure(EntityTypeBuilder<ActivityMenuOverride> builder)
    {
        builder.ToTable("ActivityMenuOverrides", "auth");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("ActivityMenuOverrideId");

        builder.Property(o => o.ActivityId).IsRequired().HasMaxLength(100);
        builder.Property(o => o.MenuItemId).IsRequired();
        builder.Property(o => o.IsVisible).IsRequired();
        builder.Property(o => o.CanEdit).IsRequired();

        builder.HasOne<MenuItem>()
            .WithMany()
            .HasForeignKey(o => o.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => new { o.ActivityId, o.MenuItemId }).IsUnique();
        builder.HasIndex(o => o.ActivityId);
    }
}
