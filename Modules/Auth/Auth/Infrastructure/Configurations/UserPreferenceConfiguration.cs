using Auth.Domain.Preferences;

namespace Auth.Infrastructure.Configurations;

public class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> builder)
    {
        builder.ToTable("UserPreferences", "auth");

        builder.HasKey(p => new { p.UserId, p.Key });

        builder.Property(p => p.Key)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Value)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(p => p.UpdatedOn)
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
