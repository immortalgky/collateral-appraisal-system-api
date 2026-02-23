using Integration.Domain.WebhookDeliveries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Integration.Infrastructure.Configurations;

public class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Payload)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.LastError)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.SubscriptionId)
            .HasDatabaseName("IX_WebhookDelivery_SubscriptionId");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_WebhookDelivery_Status");

        builder.HasIndex(x => new { x.Status, x.NextRetryAt })
            .HasDatabaseName("IX_WebhookDelivery_Status_NextRetryAt");
    }
}
