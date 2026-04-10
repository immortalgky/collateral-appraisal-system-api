using Auth.Domain.Menu;

namespace Auth.Infrastructure.Configurations;

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("MenuItems", "auth");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("MenuItemId");

        builder.Property(m => m.ItemKey).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Scope).HasConversion<int>().IsRequired();
        builder.Property(m => m.ParentId);
        builder.Property(m => m.Path).HasMaxLength(500);
        builder.Property(m => m.IconColor).HasMaxLength(100);
        builder.Property(m => m.SortOrder).IsRequired();
        builder.Property(m => m.ViewPermissionCode).IsRequired().HasMaxLength(100);
        builder.Property(m => m.EditPermissionCode).HasMaxLength(100);
        builder.Property(m => m.IsSystem).IsRequired();

        builder.OwnsOne(m => m.Icon, icon =>
        {
            icon.Property(i => i.Name).HasColumnName("IconName").IsRequired().HasMaxLength(100);
            icon.Property(i => i.Style).HasColumnName("IconStyle").HasConversion<int>().IsRequired();
        });

        // Self-referencing parent relationship
        builder.HasOne<MenuItem>()
            .WithMany()
            .HasForeignKey(m => m.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Translations (owned collection style via navigation)
        builder.HasMany(m => m.Translations)
            .WithOne()
            .HasForeignKey(t => t.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(MenuItem.Translations))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(m => m.ItemKey).IsUnique();
        builder.HasIndex(m => new { m.Scope, m.ParentId, m.SortOrder });
        // Non-unique lookup index on (Scope, Path). The plan originally called
        // for a unique filtered index, but the legacy nav intentionally allows
        // a parent and its first child to share a href (e.g. "/requests" and
        // "Request Listing" -> "/requests"), so uniqueness is not enforceable.
        builder.HasIndex(m => new { m.Scope, m.Path })
            .HasFilter("[Path] IS NOT NULL");
    }
}
