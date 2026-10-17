using Auth.Domain.Passkeys;

namespace Auth.Infrastructure.Configurations;

public class UserPasskeyConfiguration : IEntityTypeConfiguration<UserPasskey>
{
    public void Configure(EntityTypeBuilder<UserPasskey> builder)
    {
        builder.ToTable("UserPasskeys", "auth");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.UserId).IsRequired();
        // WebAuthn allows up to 1023 bytes; a nonclustered index key may be 1700.
        builder.Property(p => p.CredentialId).IsRequired().HasMaxLength(1024);
        builder.Property(p => p.PublicKey).IsRequired().HasMaxLength(1024);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(UserPasskey.MaxNameLength);
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.Transports).HasMaxLength(200);

        // The assertion's lookup, and the guard against registering one authenticator credential twice.
        builder.HasIndex(p => p.CredentialId).IsUnique();
        builder.HasIndex(p => p.UserId);

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
