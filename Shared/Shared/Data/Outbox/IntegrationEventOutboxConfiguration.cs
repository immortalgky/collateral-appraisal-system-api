using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shared.Data.Outbox;

public class IntegrationEventOutboxConfiguration : IEntityTypeConfiguration<IntegrationEventOutboxMessage>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Configure(EntityTypeBuilder<IntegrationEventOutboxMessage> builder)
    {
        builder.ToTable("IntegrationEventOutbox");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Payload)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Headers)
            .HasConversion(
                v => JsonSerializer.Serialize(v, SerializerOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, SerializerOptions)
                     ?? new Dictionary<string, string>())
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CorrelationId)
            .HasMaxLength(100);

        builder.Property(x => x.OccurredAt)
            .IsRequired();

        builder.Property(x => x.Error)
            .HasMaxLength(2000);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(x => new { x.Status, x.OccurredAt })
            .HasDatabaseName("IX_IntegrationEventOutbox_Polling");

        builder.HasIndex(x => new { x.CorrelationId, x.Status, x.OccurredAt })
            .HasDatabaseName("IX_IntegrationEventOutbox_Correlation");

        builder.HasIndex(x => new { x.Status, x.RetryCount })
            .HasDatabaseName("IX_IntegrationEventOutbox_DeadLetter");

        builder.HasIndex(x => new { x.Status, x.ProcessedAt })
            .HasDatabaseName("IX_IntegrationEventOutbox_Cleanup");
    }
}
