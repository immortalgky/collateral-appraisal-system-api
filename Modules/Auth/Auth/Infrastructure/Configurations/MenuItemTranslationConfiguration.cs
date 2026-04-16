using Auth.Domain.Menu;

namespace Auth.Infrastructure.Configurations;

public class MenuItemTranslationConfiguration : IEntityTypeConfiguration<MenuItemTranslation>
{
    public void Configure(EntityTypeBuilder<MenuItemTranslation> builder)
    {
        builder.ToTable("MenuItemTranslations", "auth");

        builder.HasKey(t => new { t.MenuItemId, t.LanguageCode });

        builder.Ignore(t => t.Id);
        builder.Property(t => t.LanguageCode).IsRequired().HasMaxLength(10);
        builder.Property(t => t.Label).IsRequired().HasMaxLength(500);
    }
}
