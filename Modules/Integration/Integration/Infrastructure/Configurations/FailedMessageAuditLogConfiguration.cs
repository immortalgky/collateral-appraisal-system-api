using Integration.Domain.FailedMessages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Integration.Infrastructure.Configurations;

public class FailedMessageAuditLogConfiguration : IEntityTypeConfiguration<FailedMessageAuditLog>
{
    public void Configure(EntityTypeBuilder<FailedMessageAuditLog> builder)
    {
        builder.ToTable("FailedMessageAuditLogs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Action).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(20).IsRequired();
        builder.Property(x => x.OutboxModule).HasMaxLength(20);
        builder.Property(x => x.ActorCode).HasMaxLength(50);
        builder.Property(x => x.IpAddress).HasMaxLength(45);
        builder.Property(x => x.Reason).HasMaxLength(500);

        // Serves the detail drawer's history lookup (design §2, GET .../failed-messages/{id}).
        builder.HasIndex(x => new { x.TargetId, x.At })
            .HasDatabaseName("IX_FailedMessageAuditLogs_Target_At");
    }
}
