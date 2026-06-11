using Auth.Domain.Configuration;

namespace Auth.Infrastructure.Configurations;

public class PasswordPolicyConfiguration : IEntityTypeConfiguration<PasswordPolicy>
{
    public void Configure(EntityTypeBuilder<PasswordPolicy> builder)
    {
        builder.ToTable("PasswordPolicy", "auth");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(p => p.RequiredLength).IsRequired();
        builder.Property(p => p.RequireDigit).IsRequired();
        builder.Property(p => p.RequireLowercase).IsRequired();
        builder.Property(p => p.RequireUppercase).IsRequired();
        builder.Property(p => p.RequireNonAlphanumeric).IsRequired();
        builder.Property(p => p.RequiredUniqueChars).IsRequired();
        builder.Property(p => p.ExpiryDays).IsRequired();
        builder.Property(p => p.HistoryCount).IsRequired();

        builder.Property(p => p.Blocklist)
            .IsRequired()
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue(string.Empty);

        builder.Property(p => p.LockoutEnabled).IsRequired();
        builder.Property(p => p.MaxFailedAccessAttempts).IsRequired();
        builder.Property(p => p.LockoutMinutes).IsRequired();
    }
}
