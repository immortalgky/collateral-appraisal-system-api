using Auth.Domain.Auditing;

namespace Auth.Infrastructure.Configurations;

public class AuthAuditLogConfiguration : IEntityTypeConfiguration<AuthAuditLog>
{
    public void Configure(EntityTypeBuilder<AuthAuditLog> builder)
    {
        builder.ToTable("AuthAuditLogs", "auth");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.OccurredAt).IsRequired();
        builder.Property(a => a.ActorUserId);
        builder.Property(a => a.ActorName).HasMaxLength(256);
        builder.Property(a => a.Action)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);
        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);
        builder.Property(a => a.EntityId);
        builder.Property(a => a.EntityName).HasMaxLength(256);
        builder.Property(a => a.ChangesJson).HasColumnType("nvarchar(max)");
        builder.Property(a => a.Workstation).HasMaxLength(256);
        builder.Property(a => a.IpAddress).HasMaxLength(64);

        builder.HasIndex(a => new { a.EntityType, a.EntityId, a.OccurredAt });
        builder.HasIndex(a => a.OccurredAt);
        builder.HasIndex(a => a.ActorUserId);
    }
}
