using Auth.Domain.Sessions;

namespace Auth.Infrastructure.Configurations;

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions", "auth");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.UserId).IsRequired();
        builder.Property(s => s.Ticket).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.LastActivityAt).IsRequired();
        builder.Property(s => s.EndReason).HasConversion<string>().HasMaxLength(30);
        builder.Property(s => s.Ip).HasMaxLength(64);
        builder.Property(s => s.UserAgent).HasMaxLength(512);

        // Revoke-by-user ("end every live session of this user") is the hot write path.
        builder.HasIndex(s => new { s.UserId, s.EndedAt });
        // The purge job scans by age.
        builder.HasIndex(s => s.LastActivityAt);
    }
}
