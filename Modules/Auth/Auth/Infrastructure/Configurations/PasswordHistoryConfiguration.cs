using Auth.Domain.Identity;

namespace Auth.Infrastructure.Configurations;

public class PasswordHistoryConfiguration : IEntityTypeConfiguration<PasswordHistory>
{
    public void Configure(EntityTypeBuilder<PasswordHistory> builder)
    {
        builder.ToTable("PasswordHistory", "auth");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.PasswordHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(h => h.CreatedAt).IsRequired();

        // Reuse check seeks the most recent rows per user.
        builder.HasIndex(h => new { h.UserId, h.CreatedAt })
            .HasDatabaseName("IX_PasswordHistory_UserId_CreatedAt");

        // Clean up history when a user is deleted.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
