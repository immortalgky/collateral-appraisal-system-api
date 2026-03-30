using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shared.Data.Outbox;

public class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessage");

        builder.HasKey(x => new { x.MessageId, x.ConsumerType });

        builder.Property(x => x.ConsumerType)
            .HasMaxLength(300);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.StartedAt)
            .IsRequired();

        builder.HasIndex(x => new { x.Status, x.StartedAt })
            .HasDatabaseName("IX_InboxMessage_StaleProcessing");

        builder.HasIndex(x => x.ProcessedAt)
            .HasDatabaseName("IX_InboxMessage_Cleanup");
    }
}
